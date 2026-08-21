using System.Windows;
using System.Windows.Controls;
using ChurchBooks.App.ViewModels;
using Microsoft.Win32;

namespace ChurchBooks.App.Views;

public partial class ImportWorkspaceView : UserControl
{
    public ImportWorkspaceView() => InitializeComponent();

    private async void ChooseFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportWorkspaceViewModel viewModel) return;
        var dialog = new OpenFileDialog
        {
            Title = "Choose data to import",
            Filter = "Supported files (*.csv;*.xls;*.xlsx)|*.csv;*.xls;*.xlsx|CSV files (*.csv)|*.csv|Excel workbooks (*.xls;*.xlsx)|*.xls;*.xlsx",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;
        try { await viewModel.LoadFileAsync(dialog.FileName); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Adaptive Smart Import", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void SaveStandardTemplateButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportWorkspaceViewModel viewModel || viewModel.SelectedStandardTemplate is null) return;
        var template = viewModel.SelectedStandardTemplate;
        var dialog = new SaveFileDialog
        {
            Title = "Save ChurchBooks template copy",
            FileName = "ChurchBooks-" + template.Name.Replace(" ", "-") + ".csv",
            Filter = "CSV files (*.csv)|*.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await viewModel.SaveStandardTemplateAsync(template, dialog.FileName);
            MessageBox.Show("Template saved. You can use it as-is or continue importing existing church spreadsheets.", "Adaptive Smart Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Adaptive Smart Import", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
