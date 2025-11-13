# SpinMonitor Desktop - Feature Roadmap

## Phase 1: Quick Wins (Weeks 1-2)

### Feature 1: Search & Filter Detections
**Priority:** HIGH | **Effort:** LOW
**Estimated Time:** 3-5 days

**Implementation:**
```csharp
// Add to MainWindow.xaml
<StackPanel Orientation="Horizontal" Margin="0,0,0,10">
    <TextBox Name="SearchBox" Width="200" PlaceholderText="Search tracks..." TextChanged="SearchBox_TextChanged"/>
    <DatePicker Name="StartDate" Margin="10,0,0,0"/>
    <DatePicker Name="EndDate" Margin="10,0,0,0"/>
    <ComboBox Name="StreamFilter" Width="150" Margin="10,0,0,0"/>
    <Button Content="Search" Click="Search_Click"/>
    <Button Content="Export" Click="Export_Click"/>
</StackPanel>
```

**Benefits:**
- Users can quickly find specific detections
- Filter by date range, stream, confidence
- Export filtered results to CSV/Excel

---

### Feature 2: Desktop Notifications
**Priority:** HIGH | **Effort:** LOW
**Estimated Time:** 2-3 days

**Implementation:**
```csharp
using Windows.UI.Notifications;

public class NotificationService
{
    public void ShowDetection(string track, string stream, double confidence)
    {
        var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
        var textElements = toastXml.GetElementsByTagName("text");
        textElements[0].AppendChild(toastXml.CreateTextNode($"Detected: {track}"));
        textElements[1].AppendChild(toastXml.CreateTextNode($"Stream: {stream} | Confidence: {confidence:0.00}"));

        var toast = new ToastNotification(toastXml);
        ToastNotificationManager.CreateToastNotifier("SpinMonitor").Show(toast);
    }
}
```

**Configuration in Settings:**
```xml
<CheckBox Content="Enable desktop notifications" IsChecked="{Binding Settings.EnableNotifications}"/>
<CheckBox Content="Only notify for high confidence (>0.7)" IsChecked="{Binding Settings.OnlyHighConfidence}"/>
<CheckBox Content="Play sound on detection" IsChecked="{Binding Settings.PlaySound}"/>
```

**Benefits:**
- Instant alerts for important detections
- No need to watch application constantly
- Configurable notification rules

---

### Feature 3: Statistics Dashboard
**Priority:** HIGH | **Effort:** MEDIUM
**Estimated Time:** 5-7 days

**Implementation:**
```csharp
// Create new StatsWindow.xaml
public class StatisticsService
{
    private readonly MySqlConnection _connection;

    public async Task<DashboardStats> GetDashboardStats(DateTime from, DateTime to)
    {
        return new DashboardStats
        {
            TotalDetections = await GetTotalDetections(from, to),
            DetectionsPerHour = await GetDetectionsPerHour(from, to),
            TopTracks = await GetTopTracks(from, to, limit: 10),
            TopStreams = await GetTopStreams(from, to, limit: 10),
            AverageConfidence = await GetAverageConfidence(from, to),
            StreamUptime = await GetStreamUptime(from, to)
        };
    }

    private async Task<int> GetTotalDetections(DateTime from, DateTime to)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM detections WHERE timestamp BETWEEN @from AND @to";
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<List<HourlyDetection>> GetDetectionsPerHour(DateTime from, DateTime to)
    {
        var results = new List<HourlyDetection>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT DATE_FORMAT(timestamp, '%Y-%m-%d %H:00') as hour, COUNT(*) as count
            FROM detections
            WHERE timestamp BETWEEN @from AND @to
            GROUP BY hour
            ORDER BY hour";
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new HourlyDetection
            {
                Hour = reader.GetDateTime(0),
                Count = reader.GetInt32(1)
            });
        }
        return results;
    }

    private async Task<List<TopTrack>> GetTopTracks(DateTime from, DateTime to, int limit)
    {
        var results = new List<TopTrack>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT track, COUNT(*) as plays, AVG(confidence) as avg_conf
            FROM detections
            WHERE timestamp BETWEEN @from AND @to
            GROUP BY track
            ORDER BY plays DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new TopTrack
            {
                Name = reader.GetString(0),
                Plays = reader.GetInt32(1),
                AverageConfidence = reader.GetDouble(2)
            });
        }
        return results;
    }
}
```

**UI (StatsWindow.xaml):**
```xml
<Window Title="Statistics Dashboard">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Date Range Selector -->
        <StackPanel Grid.Row="0" Orientation="Horizontal">
            <DatePicker Name="FromDate"/>
            <DatePicker Name="ToDate" Margin="10,0,0,0"/>
            <Button Content="Refresh" Click="Refresh_Click" Margin="10,0,0,0"/>
        </StackPanel>

        <!-- Stats Cards -->
        <Grid Grid.Row="1" Margin="0,20,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Left Column -->
            <StackPanel Grid.Column="0" Margin="0,0,10,0">
                <!-- Total Detections Card -->
                <Border Background="#F0F0F0" Padding="20" Margin="0,0,0,10">
                    <StackPanel>
                        <TextBlock Text="Total Detections" FontSize="14" Foreground="Gray"/>
                        <TextBlock Text="{Binding Stats.TotalDetections}" FontSize="32" FontWeight="Bold"/>
                    </StackPanel>
                </Border>

                <!-- Top Tracks -->
                <Border Background="#F0F0F0" Padding="20">
                    <StackPanel>
                        <TextBlock Text="Top 10 Tracks" FontSize="16" FontWeight="Bold" Margin="0,0,0,10"/>
                        <DataGrid ItemsSource="{Binding Stats.TopTracks}" AutoGenerateColumns="False">
                            <DataGrid.Columns>
                                <DataGridTextColumn Header="Track" Binding="{Binding Name}" Width="*"/>
                                <DataGridTextColumn Header="Plays" Binding="{Binding Plays}" Width="60"/>
                                <DataGridTextColumn Header="Conf" Binding="{Binding AverageConfidence, StringFormat=0.00}" Width="60"/>
                            </DataGrid.Columns>
                        </DataGrid>
                    </StackPanel>
                </Border>
            </StackPanel>

            <!-- Right Column -->
            <StackPanel Grid.Column="1" Margin="10,0,0,0">
                <!-- Average Confidence Card -->
                <Border Background="#F0F0F0" Padding="20" Margin="0,0,0,10">
                    <StackPanel>
                        <TextBlock Text="Average Confidence" FontSize="14" Foreground="Gray"/>
                        <TextBlock Text="{Binding Stats.AverageConfidence, StringFormat=0.00}" FontSize="32" FontWeight="Bold"/>
                    </StackPanel>
                </Border>

                <!-- Detections Per Hour Chart -->
                <Border Background="#F0F0F0" Padding="20">
                    <StackPanel>
                        <TextBlock Text="Detections Per Hour" FontSize="16" FontWeight="Bold" Margin="0,0,0,10"/>
                        <!-- Use LiveCharts or OxyPlot for charting -->
                        <ItemsControl ItemsSource="{Binding Stats.DetectionsPerHour}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <StackPanel Orientation="Horizontal" Margin="0,2">
                                        <TextBlock Text="{Binding Hour, StringFormat=HH:mm}" Width="50"/>
                                        <Rectangle Fill="Blue" Height="20" Width="{Binding Count}" Margin="5,0,0,0"/>
                                        <TextBlock Text="{Binding Count}" Margin="5,0,0,0"/>
                                    </StackPanel>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>
                </Border>
            </StackPanel>
        </Grid>
    </Grid>
</Window>
```

**Benefits:**
- Visual insights into detection patterns
- Identify most played tracks
- Track stream performance
- Data-driven decision making

---

### Feature 4: Dark Mode Theme
**Priority:** MEDIUM | **Effort:** LOW
**Estimated Time:** 2-3 days

**Implementation:**
```csharp
// Create ThemeManager.cs
public class ThemeManager
{
    private readonly ResourceDictionary _lightTheme;
    private readonly ResourceDictionary _darkTheme;

    public ThemeManager()
    {
        _lightTheme = new ResourceDictionary { Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative) };
        _darkTheme = new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) };
    }

    public void ApplyTheme(bool isDark)
    {
        var app = Application.Current;
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(isDark ? _darkTheme : _lightTheme);
    }
}
```

**Create Themes/DarkTheme.xaml:**
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <Color x:Key="BackgroundColor">#1E1E1E</Color>
    <Color x:Key="ForegroundColor">#FFFFFF</Color>
    <Color x:Key="AccentColor">#0078D4</Color>

    <SolidColorBrush x:Key="BackgroundBrush" Color="{StaticResource BackgroundColor}"/>
    <SolidColorBrush x:Key="ForegroundBrush" Color="{StaticResource ForegroundColor}"/>
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}"/>

    <Style TargetType="Window">
        <Setter Property="Background" Value="{StaticResource BackgroundBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}"/>
    </Style>

    <Style TargetType="TextBox">
        <Setter Property="Background" Value="#2D2D2D"/>
        <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}"/>
        <Setter Property="BorderBrush" Value="#3E3E3E"/>
    </Style>

    <Style TargetType="Button">
        <Setter Property="Background" Value="{StaticResource AccentBrush}"/>
        <Setter Property="Foreground" Value="White"/>
    </Style>
</ResourceDictionary>
```

**Add to Settings:**
```xml
<ComboBox Name="ThemeSelector" SelectionChanged="Theme_Changed">
    <ComboBoxItem Content="Light Theme" Tag="light"/>
    <ComboBoxItem Content="Dark Theme" Tag="dark"/>
    <ComboBoxItem Content="Auto (System)" Tag="auto"/>
</ComboBox>
```

**Benefits:**
- Better for night-time monitoring
- Reduces eye strain
- Modern, professional look
- Follows system theme preference

---

### Feature 5: Excel/PDF Export
**Priority:** MEDIUM | **Effort:** MEDIUM
**Estimated Time:** 4-6 days

**Implementation:**
```csharp
using ClosedXML.Excel;
using iTextSharp.text.pdf;

public class ExportService
{
    public async Task ExportToExcel(List<Detection> detections, string filePath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Detections");

        // Headers
        worksheet.Cell(1, 1).Value = "Timestamp";
        worksheet.Cell(1, 2).Value = "Stream";
        worksheet.Cell(1, 3).Value = "Track";
        worksheet.Cell(1, 4).Value = "Confidence";

        // Data
        int row = 2;
        foreach (var detection in detections)
        {
            worksheet.Cell(row, 1).Value = detection.Timestamp;
            worksheet.Cell(row, 2).Value = detection.Stream;
            worksheet.Cell(row, 3).Value = detection.Track;
            worksheet.Cell(row, 4).Value = detection.Confidence;
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add chart
        var chart = worksheet.AddChart("Detections Chart");
        chart.SetChartType(XLChartType.Column);
        chart.SetTitle("Detections Over Time");

        workbook.SaveAs(filePath);
    }

    public async Task ExportToPdf(List<Detection> detections, DashboardStats stats, string filePath)
    {
        using var document = new Document();
        using var writer = PdfWriter.GetInstance(document, new FileStream(filePath, FileMode.Create));

        document.Open();

        // Title
        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
        document.Add(new Paragraph("SpinMonitor Detection Report", titleFont));
        document.Add(new Paragraph($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}", FontFactory.GetFont(FontFactory.HELVETICA, 10)));
        document.Add(new Paragraph("\n"));

        // Summary Statistics
        document.Add(new Paragraph("Summary", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14)));
        document.Add(new Paragraph($"Total Detections: {stats.TotalDetections}"));
        document.Add(new Paragraph($"Average Confidence: {stats.AverageConfidence:0.00}"));
        document.Add(new Paragraph("\n"));

        // Detections Table
        var table = new PdfPTable(4);
        table.AddCell("Timestamp");
        table.AddCell("Stream");
        table.AddCell("Track");
        table.AddCell("Confidence");

        foreach (var detection in detections)
        {
            table.AddCell(detection.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            table.AddCell(detection.Stream);
            table.AddCell(detection.Track);
            table.AddCell(detection.Confidence.ToString("0.00"));
        }

        document.Add(table);
        document.Close();
    }
}
```

**Add to MainWindow:**
```csharp
private async void ExportExcel_Click(object sender, RoutedEventArgs e)
{
    var dialog = new SaveFileDialog
    {
        Filter = "Excel Files (*.xlsx)|*.xlsx",
        FileName = $"detections_{DateTime.Now:yyyyMMdd}.xlsx"
    };

    if (dialog.ShowDialog() == true)
    {
        var detections = await GetDetections(); // From MySQL
        await _exportService.ExportToExcel(detections, dialog.FileName);
        MessageBox.Show("Export completed!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
```

**Benefits:**
- Professional reports for clients
- Share data with stakeholders
- Archive historical data
- Easy analysis in Excel

---

## Phase 2: Professional Features (Weeks 3-4)

### Feature 6: Stream Health Monitoring
### Feature 7: Scheduled Monitoring
### Feature 8: Email Alerts
### Feature 9: Multi-Database Support
### Feature 10: Automatic Backups

---

## Phase 3: Advanced Features (Month 2)

### Feature 11: Web Dashboard
### Feature 12: Plugin System
### Feature 13: Auto-Update
### Feature 14: Machine Learning Enhancements
### Feature 15: Mobile App

---

## Implementation Timeline

| Phase | Features | Duration | Priority |
|-------|----------|----------|----------|
| Phase 1 | Search, Notifications, Stats, Dark Mode, Export | 2 weeks | HIGH |
| Phase 2 | Health Monitoring, Scheduling, Alerts | 2 weeks | MEDIUM |
| Phase 3 | Web Dashboard, ML, Plugins | 4 weeks | LOW |

---

## Dependencies to Add

```xml
<!-- Add to SpinMonitor.Desktop.csproj -->
<ItemGroup>
  <!-- For Excel Export -->
  <PackageReference Include="ClosedXML" Version="0.102.1" />

  <!-- For PDF Export -->
  <PackageReference Include="iTextSharp.LGPLv2.Core" Version="3.4.5" />

  <!-- For Charts -->
  <PackageReference Include="LiveCharts.Wpf" Version="0.9.7" />

  <!-- For Notifications -->
  <PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.1.3" />

  <!-- For Email -->
  <PackageReference Include="MailKit" Version="4.3.0" />
</ItemGroup>
```

---

## Testing Checklist

For each new feature:
- [ ] Unit tests written
- [ ] Integration tests pass
- [ ] UI tested manually
- [ ] Performance tested (no memory leaks)
- [ ] Documentation updated
- [ ] User guide updated
- [ ] Settings UI updated (if needed)

---

## Next Steps

1. Choose features from Phase 1 to implement
2. Create feature branches: `git checkout -b feature/search-filter`
3. Implement feature
4. Test thoroughly
5. Create pull request
6. Merge to main
7. Repeat for next feature

---

**Version:** 1.1.0 (with Phase 1 features)
**Target Release:** 2 weeks from now
