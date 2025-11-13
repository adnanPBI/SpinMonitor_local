using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SpinMonitor.Views
{
    public partial class DbViewerWindow : Window
    {
        private readonly string _dbPath;
        private int _pageIndex = 0;
        private int _pageSize = 500;
        private int _totalRows = 0;

        public DbViewerWindow(string dbPath)
        {
            InitializeComponent();
            _dbPath = dbPath;
            DbPathBox.Text = _dbPath;
            LoadTables();
        }

        private SqliteConnection OpenReadOnly()
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            var conn = new SqliteConnection(cs);
            conn.Open();
            return conn;
        }

        private void LoadTables()
        {
            try
            {
                using var conn = OpenReadOnly();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                using var rdr = cmd.ExecuteReader();

                TablesCombo.Items.Clear();
                while (rdr.Read()) TablesCombo.Items.Add(rdr.GetString(0));

                if (TablesCombo.Items.Count > 0)
                    TablesCombo.SelectedIndex = 0;

                StatusText.Text = $"Found {TablesCombo.Items.Count} tables.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading tables: {ex.Message}";
                System.Windows.MessageBox.Show(this, ex.Message, "DB Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (string whereSql, SqliteParameter[] parameters) BuildWhereClause(SqliteConnection conn, string table, string filter)
        {
            filter = (filter ?? "").Trim();
            if (string.IsNullOrEmpty(filter))
                return ("", Array.Empty<SqliteParameter>());

            var pragma = new DataTable();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info([{table}]);";
                using var rdr = cmd.ExecuteReader();
                pragma.Load(rdr);
            }

            var cols = pragma.AsEnumerable()
                             .Select(r => new {
                                 Name = r.Field<string>("name") ?? "",
                                 Type = (r.Field<string>("type") ?? "").ToUpperInvariant()
                             })
                             .Where(c => !string.IsNullOrEmpty(c.Name))
                             .ToList();

            var likeParam = new SqliteParameter("@p", $"%{filter}%");
            var textCols = cols.Where(c => c.Type.Contains("CHAR") || c.Type.Contains("TEXT") || c.Type.Contains("CLOB"))
                               .Select(c => $"[{c.Name}] LIKE @p")
                               .ToList();

            string predicate;
            if (textCols.Count > 0)
            {
                predicate = string.Join(" OR ", textCols);
            }
            else
            {
                var allCols = cols.Select(c => $"CAST([{c.Name}] AS TEXT) LIKE @p");
                predicate = string.Join(" OR ", allCols);
            }

            if (string.IsNullOrWhiteSpace(predicate))
                return ("", Array.Empty<SqliteParameter>());

            return ($"WHERE {predicate}", new[] { likeParam });
        }

        private void LoadPage()
        {
            if (TablesCombo.SelectedItem is not string table || string.IsNullOrWhiteSpace(table))
                return;

            try
            {
                using var conn = OpenReadOnly();

                var (whereSql, whereParams) = BuildWhereClause(conn, table, FilterBox.Text ?? "");

                using (var countCmd = conn.CreateCommand())
                {
                    countCmd.CommandText = $"SELECT COUNT(*) FROM [{table}] {whereSql};";
                    foreach (var p in whereParams) countCmd.Parameters.Add(p);
                    _totalRows = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                }

                var maxPage = Math.Max(0, (_totalRows - 1) / _pageSize);
                if (_pageIndex > maxPage) _pageIndex = maxPage;
                if (_pageIndex < 0) _pageIndex = 0;

                var offset = _pageIndex * _pageSize;

                var sql = $"SELECT * FROM [{table}] {whereSql} ORDER BY rowid DESC LIMIT @_limit OFFSET @_offset;";
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                foreach (var p in whereParams) cmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
                cmd.Parameters.Add(new SqliteParameter("@_limit", _pageSize));
                cmd.Parameters.Add(new SqliteParameter("@_offset", offset));

                var dt = new DataTable();
                using (var rdr = cmd.ExecuteReader())
                    dt.Load(rdr);

                Grid.ItemsSource = dt.DefaultView;

                var from = _totalRows == 0 ? 0 : offset + 1;
                var to   = Math.Min(offset + _pageSize, _totalRows);
                var maxPageDisplay = (Math.Max(0, (_totalRows - 1) / _pageSize) + 1);
                PageInfo.Text = $"Page {_pageIndex + 1}/{maxPageDisplay}  •  Showing {from}-{to} of {_totalRows}";
                StatusText.Text = $"{table}: {dt.Rows.Count} row(s) loaded.";
                PrevBtn.IsEnabled = _pageIndex > 0;
                NextBtn.IsEnabled = _pageIndex < Math.Max(0, (_totalRows - 1) / _pageSize);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading data: {ex.Message}";
                System.Windows.MessageBox.Show(this, ex.Message, "Query Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TablesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            _pageIndex = 0;
            LoadPage();
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            _pageIndex--;
            LoadPage();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            _pageIndex++;
            LoadPage();
        }

        private void PageSizeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PageSizeBox.Text, out var ps) && ps > 0)
            {
                _pageSize = ps;
                _pageIndex = 0;
                LoadPage();
            }
            else
            {
                PageSizeBox.Text = _pageSize.ToString();
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _pageIndex = 0;
            LoadPage();
        }

        private void FilterBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _pageIndex = 0;
                LoadPage();
                e.Handled = true;
            }
        }
    }
}