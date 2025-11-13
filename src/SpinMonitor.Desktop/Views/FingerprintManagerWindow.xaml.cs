using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;

namespace RadioMonitor
{
    public partial class FingerprintManagerWindow : Window
    {
        private readonly string _dbPath;
        private DataView? _currentView;

        public FingerprintManagerWindow(string dbPath)
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
                while (rdr.Read())
                    TablesCombo.Items.Add(rdr.GetString(0));

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

        private static DataTable ExecuteToTable(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var rdr = cmd.ExecuteReader();

            var table = new DataTable();
            table.Load(rdr);
            return table;
        }

        private void LoadTableData(string table, int limit)
        {
            try
            {
                using var conn = OpenReadOnly();

                string[] tryCols = { "CreatedUtc", "InsertedUtc", "UpdatedUtc", "TimestampUtc", "ts", "created_at" };
                string orderBy = "rowid DESC";
                try
                {
                    var pragma = ExecuteToTable(conn, $"PRAGMA table_info([{table}]);");
                    var cols = pragma.AsEnumerable().Select(r => r["name"]?.ToString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var first = tryCols.FirstOrDefault(c => cols.Contains(c));
                    if (!string.IsNullOrEmpty(first)) orderBy = $"[{first}] DESC";
                }
                catch { }

                var sql = $"SELECT * FROM [{table}] ORDER BY {orderBy} LIMIT {limit};";
                var dt = ExecuteToTable(conn, sql);
                _currentView = new DataView(dt);
                Grid.ItemsSource = _currentView;
                StatusText.Text = $"{table}: {dt.Rows.Count} row(s) loaded (limit {limit}).";
                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading data: {ex.Message}";
                System.Windows.MessageBox.Show(this, ex.Message, "Query Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (TablesCombo.SelectedItem is string table &&
                int.TryParse(LimitBox.Text, out var limit) && limit > 0)
            {
                LoadTableData(table, limit);
            }
        }

        private void TablesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && TablesCombo.SelectedItem is string table &&
                int.TryParse(LimitBox.Text, out var limit) && limit > 0)
            {
                LoadTableData(table, limit);
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_currentView == null) return;

            var term = (FilterBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(term))
            {
                _currentView.RowFilter = string.Empty;
                StatusText.Text = $"{TablesCombo.SelectedItem}: {_currentView.Count} row(s) shown.";
                return;
            }

            var dt = _currentView.Table;
            var stringCols = dt.Columns.Cast<DataColumn>()
                .Where(c => c.DataType == typeof(string))
                .Select(c => $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{term.Replace("'", "''")}%'");
            _currentView.RowFilter = string.Join(" OR ", stringCols);
            StatusText.Text = $"{TablesCombo.SelectedItem}: {_currentView.Count} row(s) after filter.";
        }
    }
}
