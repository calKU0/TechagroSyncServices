using ServiceManager.Enums;
using ServiceManager.Helpers;
using ServiceManager.Models;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ServiceManager
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<LogFileItem> logFiles = new ObservableCollection<LogFileItem>();
        private DispatcherTimer refreshTimer;
        private ServiceController _serviceController;
        private FileSystemWatcher _logWatcher;
        public ObservableCollection<ServiceItem> AvailableServices { get; } = new ObservableCollection<ServiceItem>();
        private ServiceItem? _selectedService;
        private const int InitialTailLines = 5000;
        private const int PageLines = 5000;

        private BulkObservableCollection<LogLine> _currentLogLines = new();
        private BulkObservableCollection<LogLine> _filteredLogLines = new();
        private string? _currentPath;
        private long _loadedStartOffset = 0;
        private bool _isLoadingMore = false;
        private bool _reachedFileStart = false;
        private long _lastReadOffset = 0;
        private object _lastSelectedLog;
        private StackPanel MarginRangesPanel = new StackPanel { Margin = new Thickness(6) };
        private List<(TextBox Min, TextBox Max, TextBox Margin)> MarginTextBoxes = new();
        private decimal _defaultMargin = 25;
        private List<MarginRange> _marginRanges = new();

        public MainWindow()
        {
            InitializeComponent();
            IcAvailableServices.ItemsSource = AvailableServices;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAvailableServices();

            if (AvailableServices.Count == 1)
            {
                SelectService(AvailableServices[0]);
                ServiceSelectionOverlay.Visibility = Visibility.Collapsed;
                MainContentAreaNav.Visibility = Visibility.Visible;
            }
            else
            {
                ServiceSelectionOverlay.Visibility = Visibility.Visible;
                MainContentAreaNav.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadEntireFileWithFilterAsync()
        {
            if (LvLogFiles.SelectedItem is not LogFileItem item || !File.Exists(item.Path))
                return;

            _currentLogLines.Clear();

            // Inicjalizujemy zmienną
            string[] allLines = Array.Empty<string>();

            // Czytamy plik w tle
            await Task.Run(() =>
            {
                using var fs = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);

                var lines = new List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }

                allLines = lines.ToArray();
            });

            // Filtrujemy linie
            var filteredLines = allLines.Select(ParseLogLine)
                                        .Where(l => l.Level == LogLevel.Error || l.Level == LogLevel.Warning);

            _currentLogLines.AddRange(filteredLines);

            ApplyFilter(); // Aktualizujemy widok
        }

        private void InitLogWatcher()
        {
            // Dispose previous watcher if it exists
            if (_logWatcher != null)
            {
                _logWatcher.EnableRaisingEvents = false;
                _logWatcher.Created -= (s, e) => Dispatcher.Invoke(LoadLogFiles);
                _logWatcher.Deleted -= (s, e) => Dispatcher.Invoke(LoadLogFiles);
                _logWatcher.Dispose();
                _logWatcher = null;
            }

            if (string.IsNullOrEmpty(_selectedService?.LogFolderPath) || !Directory.Exists(_selectedService.LogFolderPath))
                return;

            _logWatcher = new FileSystemWatcher(_selectedService.LogFolderPath, "*.txt")
            {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            _logWatcher.Created += (s, e) => Dispatcher.Invoke(LoadLogFiles);
            _logWatcher.Deleted += (s, e) => Dispatcher.Invoke(LoadLogFiles);
        }

        private void LoadAvailableServices()
        {
            AvailableServices.Clear();

            var keys = ConfigurationManager.AppSettings.AllKeys
                .Where(k => k.StartsWith("Service_"))
                .Select(k => k.Split('_')[1])
                .Distinct();

            foreach (var key in keys)
            {
                var service = new ServiceItem
                {
                    Id = key,
                    Name = ConfigurationManager.AppSettings[$"Service_{key}_Name"] ?? key,
                    LogoPath = ConfigurationManager.AppSettings[$"Service_{key}_LogoPath"] ?? "",
                    ServiceName = ConfigurationManager.AppSettings[$"Service_{key}_ServiceName"] ?? "",
                    LogFolderPath = ConfigurationManager.AppSettings[$"Service_{key}_LogFolder"] ?? "",
                    ExternalConfigPath = ConfigurationManager.AppSettings[$"Service_{key}_ConfigPath"] ?? ""
                };
                AvailableServices.Add(service);
            }

            CbServiceSelector.ItemsSource = AvailableServices;
        }

        private void SelectService(ServiceItem service)
        {
            if (service == null) return;

            _selectedService = service;
            _serviceController = new ServiceController(service.ServiceName);

            InitLogWatcher();
            RefreshServiceStatus();
            LoadLogFiles();
            LoadConfig();
            _currentLogLines.Clear();
            ServiceNameTextBox.Text = service.Name;
        }

        private void ServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ServiceItem service)
            {
                SelectService(service);
                CbServiceSelector.SelectedValue = service.Id;
                ServiceSelectionOverlay.Visibility = Visibility.Collapsed;
                CbServiceSelector.Visibility = Visibility.Visible;
                MainContentAreaNav.Visibility = Visibility.Visible;
            }
        }

        private void CbServiceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbServiceSelector.SelectedItem is ServiceItem selected)
            {
                SelectService(selected);

                // Make sure UI panels update correctly
                ServiceSelectionOverlay.Visibility = Visibility.Collapsed;
                CbServiceSelector.Visibility = Visibility.Visible;
                MainContentAreaNav.Visibility = Visibility.Visible;

                MainContentArea.Visibility = Visibility.Collapsed;
                LogsViewContainer.Visibility = Visibility.Collapsed;
                ConfigViewContainer.Visibility = Visibility.Collapsed;
                BtnShowLogs.IsChecked = false;
                BtnShowConfig.IsChecked = false;
            }
        }

        private void BtnShowLogs_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Visibility = Visibility.Visible;
            LogsViewContainer.Visibility = Visibility.Visible;
            ConfigViewContainer.Visibility = Visibility.Collapsed;

            LvLogFiles.ItemsSource = logFiles;
            IcLogLines.ItemsSource = _filteredLogLines;

            HookLogLinesScrollViewer();

            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer.Tick -= RefreshTimer_Tick;
            }
            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();

            LoadLogFiles();
        }

        private void BtnShowConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            MainContentArea.Visibility = Visibility.Visible;
            LogsViewContainer.Visibility = Visibility.Collapsed;
            ConfigViewContainer.Visibility = Visibility.Visible;
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshServiceStatus();

            if (LogsViewContainer.Visibility != Visibility.Visible ||
                LvLogFiles.SelectedItem is not LogFileItem item ||
                string.IsNullOrEmpty(_currentPath)) return;

            var listBox = IcLogLines;
            if (listBox.Items.Count == 0) return;

            // Check if user is at bottom
            var sv = FindVisualChilds.FindVisualChild<ScrollViewer>(listBox);
            bool isAtBottom = sv != null &&
                              Math.Abs(sv.VerticalOffset - sv.ScrollableHeight) < 2;

            try
            {
                var newLines = await Task.Run(() => LogFileReader.ReadNewLines(_currentPath!, ref _lastReadOffset));
                if (newLines.Count > 0)
                {
                    // update in-memory log lines
                    _currentLogLines.AddRange(newLines.Select(ParseLogLine));
                    ApplyFilter();
                    // update warning/error counters
                    int newWarnings = newLines.Count(l => l.Contains("WRN]", StringComparison.Ordinal));
                    int newErrors = newLines.Count(l => l.Contains("ERR]", StringComparison.Ordinal));

                    item.WarningsCount += newWarnings;
                    item.ErrorsCount += newErrors;

                    // scroll to bottom if user was at bottom
                    if (isAtBottom && sv != null)
                    {
                        await Dispatcher.BeginInvoke(() =>
                        {
                            listBox.ScrollIntoView(listBox.Items[^1]);
                        }, DispatcherPriority.Background);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd odczytu logu {item.Name}: {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            if (_selectedService == null) return;

            try
            {
                var map = new ExeConfigurationFileMap { ExeConfigFilename = _selectedService.ExternalConfigPath };
                var config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);

                // --- Reset głównego panelu ---
                ConfigStackPanel.Children.Clear();
                MarginTextBoxes.Clear();
                _marginRanges.Clear();

                var groupedFields = ConfigHelper.AllFields.GroupBy(f => f.Group);

                LoadConnectionStrings(config);
                LoadAppSettings(config, groupedFields);
                LoadMargins(config);

                // --- Save button ---
                var saveButton = new Button
                {
                    Content = "Zapisz",
                    Margin = new Thickness(6, 12, 6, 24),
                    Padding = new Thickness(12, 6, 12, 6),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x35, 0x7A, 0xBD)),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Width = 120
                };
                saveButton.Click += BtnSaveConfig_Click;
                ConfigStackPanel.Children.Add(saveButton);

                ConfigViewContainer.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się załadować konfiguracji: {ex.Message}");
            }
        }

        private void LoadConnectionStrings(Configuration config)
        {
            foreach (ConnectionStringSettings conn in config.ConnectionStrings.ConnectionStrings)
            {
                if (conn.Name == "LocalSqlServer") continue;

                var groupBoxDatabase = new GroupBox { Header = "Baza danych", Margin = new Thickness(0, 6, 0, 6) };
                var groupPanel = new StackPanel { Margin = new Thickness(6) };

                var connParts = ConfigHelper.ParseConnectionString(conn.ConnectionString)
                                            .Where(kv => !ConfigHelper.ExcludedConnectionStringKeys.Contains(kv.Key))
                                            .ToDictionary(kv => kv.Key, kv => kv.Value);

                foreach (var part in connParts)
                {
                    groupPanel.Children.Add(CreateLabelTextBoxRow(
                        ConfigHelper.ConnectionStringKeyTranslations.TryGetValue(part.Key, out var translated) ? translated : part.Key,
                        part.Value,
                        $"{conn.Name}|{part.Key}"));
                }

                groupBoxDatabase.Content = groupPanel;
                ConfigStackPanel.Children.Add(groupBoxDatabase);
            }
        }

        private void LoadAppSettings(Configuration config, IEnumerable<IGrouping<string, ConfigField>> groupedFields)
        {
            foreach (var group in groupedFields)
            {
                var existingFields = group.Where(f => config.AppSettings.Settings.AllKeys.Contains(f.Key)).ToList();
                if (!existingFields.Any()) continue;

                var groupBox = new GroupBox { Header = group.Key, Margin = new Thickness(0, 6, 0, 6) };
                var panel = new StackPanel { Margin = new Thickness(6) };

                foreach (var field in existingFields)
                {
                    if (field.Key == "MarginRanges") continue;

                    string value = config.AppSettings.Settings[field.Key]?.Value ?? "";
                    panel.Children.Add(CreateLabelTextBoxRow(field.Label, value, field.Key, field.IsEnabled));
                }

                groupBox.Content = panel;
                ConfigStackPanel.Children.Add(groupBox);
            }
        }

        private void LoadMargins(Configuration config)
        {
            string defaultMarginValue = config.AppSettings.Settings["DefaultMargin"]?.Value ?? "10";
            _defaultMargin = decimal.Parse(defaultMarginValue);

            string rangesValue = config.AppSettings.Settings["MarginRanges"]?.Value ?? "";
            _marginRanges = ConfigHelper.ParseMarginRanges(rangesValue);

            var groupBoxMargins = new GroupBox { Header = "Marże", Margin = new Thickness(0, 6, 0, 6) };
            var groupPanel = new StackPanel { Margin = new Thickness(6) };

            // --- Marża podstawowa ---
            var defaultMarginGrid = new Grid();
            defaultMarginGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            defaultMarginGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var defaultMarginLabel = new TextBlock { Text = "Marża podstawowa (%)", VerticalAlignment = VerticalAlignment.Center };
            var defaultMarginTextBox = new TextBox { Text = _defaultMargin.ToString(), Name = "DefaultMarginTextBox", Margin = new Thickness(0, 4, 0, 4) };
            Grid.SetColumn(defaultMarginLabel, 0);
            Grid.SetColumn(defaultMarginTextBox, 1);
            defaultMarginGrid.Children.Add(defaultMarginLabel);
            defaultMarginGrid.Children.Add(defaultMarginTextBox);

            groupPanel.Children.Add(defaultMarginGrid);

            // --- Przedziały marży ---
            var marginRangesPanel = new StackPanel { Margin = new Thickness(6) };

            // Labelki kolumn
            var headerGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.Children.Add(new TextBlock { Text = "Od", FontWeight = FontWeights.Bold, Margin = new Thickness(2) });
            Grid.SetColumn(headerGrid.Children[headerGrid.Children.Count - 1], 0);
            headerGrid.Children.Add(new TextBlock { Text = "Do", FontWeight = FontWeights.Bold, Margin = new Thickness(2) });
            Grid.SetColumn(headerGrid.Children[headerGrid.Children.Count - 1], 1);
            headerGrid.Children.Add(new TextBlock { Text = "Marża (%)", FontWeight = FontWeights.Bold, Margin = new Thickness(2) });
            Grid.SetColumn(headerGrid.Children[headerGrid.Children.Count - 1], 2);

            marginRangesPanel.Children.Add(headerGrid);

            foreach (var range in _marginRanges)
            {
                AddMarginRangeRowToPanel(marginRangesPanel, range.Min, range.Max, range.Margin);
            }

            var addButton = new Button { Content = "Dodaj przedział", Margin = new Thickness(0, 6, 0, 6) };
            addButton.Click += (s, e) => AddMarginRangeRowToPanel(marginRangesPanel, 0, 0, 0);

            groupPanel.Children.Add(marginRangesPanel);
            groupPanel.Children.Add(addButton);

            groupBoxMargins.Content = groupPanel;
            ConfigStackPanel.Children.Add(groupBoxMargins);
        }

        private Grid CreateLabelTextBoxRow(string labelText, string textBoxValue, string tag, bool isEnabled = true)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock { Text = labelText, Margin = new Thickness(0, 4, 0, 4), VerticalAlignment = VerticalAlignment.Center };
            var textbox = new TextBox { Text = textBoxValue, Margin = new Thickness(0, 4, 0, 4), Tag = tag, IsEnabled = isEnabled };

            Grid.SetColumn(label, 0);
            Grid.SetColumn(textbox, 1);
            grid.Children.Add(label);
            grid.Children.Add(textbox);

            return grid;
        }

        private void AddMarginRangeRowToPanel(StackPanel panel, decimal min, decimal max, decimal margin)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var minBox = new TextBox { Text = min.ToString(), Margin = new Thickness(2) };
            var maxBox = new TextBox { Text = max.ToString(), Margin = new Thickness(2) };
            var marginBox = new TextBox { Text = margin.ToString(), Margin = new Thickness(2) };

            Grid.SetColumn(minBox, 0);
            Grid.SetColumn(maxBox, 1);
            Grid.SetColumn(marginBox, 2);

            // --- Ładny przycisk X ---
            var removeButton = new Button
            {
                Width = 24,
                Height = 24,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Margin = new Thickness(2),
                ToolTip = "Usuń przedział",
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };

            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0 0 L 8 8 M 8 0 L 0 8"), // prosty krzyż
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Stretch = Stretch.Uniform
            };

            removeButton.Content = path;

            removeButton.Click += (s, e) =>
            {
                panel.Children.Remove(grid);
                MarginTextBoxes.Remove((minBox, maxBox, marginBox));
            };

            Grid.SetColumn(removeButton, 3);

            grid.Children.Add(minBox);
            grid.Children.Add(maxBox);
            grid.Children.Add(marginBox);
            grid.Children.Add(removeButton);

            panel.Children.Add(grid);
            MarginTextBoxes.Add((minBox, maxBox, marginBox));
        }

        private void BtnReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadConfig();
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedService == null) return;

            try
            {
                var map = new ExeConfigurationFileMap { ExeConfigFilename = _selectedService.ExternalConfigPath };
                var config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);

                // Zapis podstawowej marży
                var defaultMarginTextBox = ConfigStackPanel.Children.OfType<Grid>()
                    .SelectMany(g => g.Children.OfType<TextBox>())
                    .FirstOrDefault(tb => tb.Name == "DefaultMarginTextBox");

                if (defaultMarginTextBox != null)
                    _defaultMargin = decimal.Parse(defaultMarginTextBox.Text);

                if (config.AppSettings.Settings["DefaultMargin"] != null)
                    config.AppSettings.Settings["DefaultMargin"].Value = _defaultMargin.ToString();
                else
                    config.AppSettings.Settings.Add("DefaultMargin", _defaultMargin.ToString());

                // Zapis przedziałów
                _marginRanges = MarginTextBoxes.Select(t =>
                    new MarginRange
                    {
                        Min = decimal.Parse(t.Min.Text),
                        Max = decimal.Parse(t.Max.Text),
                        Margin = decimal.Parse(t.Margin.Text)
                    }).ToList();

                string serialized = ConfigHelper.SerializeMarginRanges(_marginRanges);

                if (config.AppSettings.Settings["MarginRanges"] != null)
                    config.AppSettings.Settings["MarginRanges"].Value = serialized;
                else
                    config.AppSettings.Settings.Add("MarginRanges", serialized);

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                MessageBox.Show("Konfiguracja zapisana.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się zapisać konfiguracji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadLogFiles()
        {
            logFiles.Clear();
            if (!Directory.Exists(_selectedService.LogFolderPath)) return;

            try
            {
                var files = Directory.GetFiles(_selectedService.LogFolderPath, "*.txt")
                    .Select(filePath =>
                    {
                        int warnings = 0;
                        int errors = 0;

                        try
                        {
                            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var sr = new StreamReader(fs))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    if (line.Contains("WRN]", StringComparison.Ordinal)) warnings++;
                                    if (line.Contains("ERR]", StringComparison.Ordinal)) errors++;
                                }
                            }

                            string fileName = Path.GetFileNameWithoutExtension(filePath);
                            string datePart = fileName.Replace("log-", "");

                            string formattedDate = fileName;
                            DateTime? parsedDate = null;
                            if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime dt))
                            {
                                formattedDate = dt.ToString("dd.MM.yyyy");
                                parsedDate = dt;
                            }

                            return new LogFileItem
                            {
                                Name = formattedDate,
                                Path = filePath,
                                WarningsCount = warnings,
                                ErrorsCount = errors,
                                Date = parsedDate ?? DateTime.MinValue // add Date property in LogFileItem
                            };
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(f => f != null)
                    .OrderByDescending(f => f.Date) // latest first
                    .ToList();

                foreach (var f in files)
                    logFiles.Add(f);

                // Auto-select latest file
                if (logFiles.Count > 0)
                {
                    LvLogFiles.SelectedItem = logFiles[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się załadować listy plików logów: {ex.Message}");
            }
        }

        private void IcLogLines_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalOffset <= 0 && ChkShowOnlyWarningsAndErrors.IsChecked == false)
                _ = LoadMoreAsync();
        }

        private void HookLogLinesScrollViewer()
        {
            // Use Dispatcher to ensure layout is ready
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var sv = GetScrollViewer(IcLogLines);
                if (sv != null)
                {
                    sv.ScrollChanged -= IcLogLines_ScrollChanged; // prevent double hook
                    sv.ScrollChanged += IcLogLines_ScrollChanged;
                }
                else
                {
                    Debug.WriteLine("ScrollViewer still null! Wait until ListBox is visible.");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private ScrollViewer? GetScrollViewer(DependencyObject dep)
        {
            if (dep is ScrollViewer viewer)
                return viewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = VisualTreeHelper.GetChild(dep, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private async void LoadSelectedFileContent()
        {
            _currentLogLines.Clear();
            if (LvLogFiles.SelectedItem is not LogFileItem item || !File.Exists(item.Path))
                return;

            _currentPath = item.Path;
            try
            {
                _lastReadOffset = new FileInfo(item.Path).Length;

                var (lines, startOffset, reachedStart) =
                    await Task.Run(() => LogFileReader.ReadLastLines(item.Path, InitialTailLines));

                _loadedStartOffset = startOffset;
                _reachedFileStart = reachedStart;

                _currentLogLines.AddRange(lines.Select(ParseLogLine));
                ApplyFilter();

                await Dispatcher.BeginInvoke(() =>
                {
                    if (IcLogLines.Items.Count > 0)
                    {
                        IcLogLines.UpdateLayout();
                        IcLogLines.ScrollIntoView(IcLogLines.Items[^1]); // bottom
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd odczytu logu {item.Name}: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            _filteredLogLines.Clear();

            bool filter = ChkShowOnlyWarningsAndErrors.IsChecked == true;

            foreach (var line in _currentLogLines)
            {
                if (!filter || line.Level == LogLevel.Warning || line.Level == LogLevel.Error)
                    _filteredLogLines.Add(line);
            }

            if (_filteredLogLines.Count > 0)
                IcLogLines.ScrollIntoView(_filteredLogLines[^1]);
        }

        private async Task LoadMoreAsync()
        {
            if (_isLoadingMore || _reachedFileStart || string.IsNullOrEmpty(_currentPath)) return;
            _isLoadingMore = true;

            try
            {
                var anchor = IcLogLines.Items.Count > 0 ? IcLogLines.Items[0] : null;

                var (older, newStart, reachedStart) =
                    await Task.Run(() => LogFileReader.ReadPreviousLines(_currentPath!, _loadedStartOffset, PageLines));

                if (older.Count > 0)
                {
                    _currentLogLines.InsertRange(0, older.Select(ParseLogLine));
                    ApplyFilter();
                    _loadedStartOffset = newStart;
                    _reachedFileStart = reachedStart;

                    if (anchor != null)
                    {
                        IcLogLines.UpdateLayout();
                        IcLogLines.ScrollIntoView(anchor); // keep position
                    }
                }
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        private async void ChkShowOnlyWarningsAndErrors_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkShowOnlyWarningsAndErrors.IsChecked == true)
            {
                await LoadEntireFileWithFilterAsync();
            }
            else
            {
                LoadSelectedFileContent();
            }
        }

        private LogLine ParseLogLine(string line)
        {
            var level = LogLevel.Information;
            if (line.Contains("ERR]", StringComparison.Ordinal)) level = LogLevel.Error;
            else if (line.Contains("WRN]", StringComparison.Ordinal)) level = LogLevel.Warning;
            return new LogLine { Level = level, Message = line };
        }

        private void LvLogFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvLogFiles.SelectedItem == null)
            {
                if (_lastSelectedLog != null)
                {
                    LvLogFiles.SelectedItem = _lastSelectedLog;
                }
                return;
            }

            _lastSelectedLog = LvLogFiles.SelectedItem;

            TxtSelectedFileName.Text = ((LogFileItem)LvLogFiles.SelectedItem).Name;
            LoadSelectedFileContent();
        }

        private void RefreshServiceStatus()
        {
            try
            {
                _serviceController.Refresh();

                switch (_serviceController.Status)
                {
                    case ServiceControllerStatus.Running:
                        ServiceStatusDot.Fill = Brushes.Green;
                        ServiceStatusText.Text = "Online";
                        BtnStartService.IsEnabled = false;
                        BtnStopService.IsEnabled = true;
                        BtnRestartService.IsEnabled = true;
                        break;

                    case ServiceControllerStatus.Stopped:
                        ServiceStatusDot.Fill = Brushes.Red;
                        ServiceStatusText.Text = "Offline";
                        BtnStartService.IsEnabled = true;
                        BtnStopService.IsEnabled = false;
                        BtnRestartService.IsEnabled = false;
                        break;

                    case ServiceControllerStatus.Paused:
                        ServiceStatusDot.Fill = Brushes.Orange;
                        ServiceStatusText.Text = "Paused";
                        BtnStartService.IsEnabled = true;
                        BtnStopService.IsEnabled = true;
                        BtnRestartService.IsEnabled = true;
                        break;

                    default: // Pending states
                        ServiceStatusDot.Fill = Brushes.Gray;
                        ServiceStatusText.Text = _serviceController.Status.ToString();
                        BtnStartService.IsEnabled = false;
                        BtnStopService.IsEnabled = false;
                        BtnRestartService.IsEnabled = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                ServiceStatusDot.Fill = Brushes.Gray;
                ServiceStatusText.Text = "Error";
                BtnStartService.IsEnabled = BtnStopService.IsEnabled = BtnRestartService.IsEnabled = false;
                MessageBox.Show($"Nie udało się sprawdzić statusu usługi: {ex.Message}");
            }
        }

        private void BtnStartService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _serviceController.Start();
                _serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy uruchamianiu usługi: {ex.Message}");
            }
            RefreshServiceStatus();
        }

        private void BtnStopService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _serviceController.Stop();
                _serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy zatrzymywaniu usługi: {ex.Message}");
            }
            RefreshServiceStatus();
        }

        private void BtnRestartService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _serviceController.Stop();
                _serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));

                _serviceController.Start();
                _serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy restartowaniu usługi: {ex.Message}");
            }
            RefreshServiceStatus();
        }
    }
}