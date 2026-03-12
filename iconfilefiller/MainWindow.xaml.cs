using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace IconFileFiller;

public partial class MainWindow : Window
{
    private readonly List<string> _pngFiles = [];

    public MainWindow()
    {
        InitializeComponent();
    }

    // --- Drag and Drop ---

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            bool hasAnyPng = files.Any(f => IsImageFile(f));
            e.Effects = hasAnyPng ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var pngFiles = files.Where(f => IsImageFile(f));

        int added = 0;
        foreach (string file in pngFiles)
        {
            if (!_pngFiles.Contains(file))
            {
                _pngFiles.Add(file);
                added++;
            }
        }

        RefreshFileList();
        StatusText.Text = added > 0
            ? $"Added {added} file(s). {_pngFiles.Count} total."
            : "No new image files to add (duplicates skipped).";
    }

    // --- Browse Button ---

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files (*.png;*.bmp)|*.png;*.bmp",
            Multiselect = true,
            Title = "Select PNG or BMP files to convert"
        };

        if (dialog.ShowDialog() != true)
            return;

        int added = 0;
        foreach (string file in dialog.FileNames)
        {
            if (!_pngFiles.Contains(file))
            {
                _pngFiles.Add(file);
                added++;
            }
        }

        RefreshFileList();
        StatusText.Text = $"Added {added} file(s). {_pngFiles.Count} total.";
    }

    // --- Context Menu ---

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = FileListBox.SelectedItems.Cast<string>().ToList();
        foreach (string item in selected)
            _pngFiles.Remove(item);

        RefreshFileList();
        StatusText.Text = $"Removed {selected.Count} file(s). {_pngFiles.Count} remaining.";
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        ClearFileList();
    }

    private void ClearListButton_Click(object sender, RoutedEventArgs e)
    {
        ClearFileList();
    }

    private void ClearFileList()
    {
        _pngFiles.Clear();
        RefreshFileList();
        StatusText.Text = "Cleared all files.";
    }

    // --- Convert ---

    private void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pngFiles.Count == 0)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "Icon Files (*.ico)|*.ico",
            FileName = Path.GetFileNameWithoutExtension(_pngFiles[0]) + ".ico",
            Title = "Save ICO file"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            if (_pngFiles.Count == 1)
            {
                // Single PNG: auto-generate standard sizes (16, 32, 48, 256)
                IcoWriter.CreateIco(_pngFiles[0], dialog.FileName);
            }
            else
            {
                // Multiple PNGs: each becomes an entry in the same ICO file
                IcoWriter.CreateIcoFromMultiple(_pngFiles, dialog.FileName);
            }

            StatusText.Text = $"Saved: {dialog.FileName}";
            MessageBox.Show(
                $"ICO file created with {_pngFiles.Count} image(s)!\n\n{dialog.FileName}",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to create ICO:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // --- Helpers ---

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshFileList()
    {
        FileListBox.ItemsSource = null;
        FileListBox.ItemsSource = _pngFiles;
        bool hasFiles = _pngFiles.Count > 0;
        ConvertButton.IsEnabled = hasFiles;
        ClearListButton.IsEnabled = hasFiles;
    }
}
