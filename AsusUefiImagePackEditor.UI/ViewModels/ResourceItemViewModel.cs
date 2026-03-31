using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Win32;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

using AsusUefiImagePackEditor.Core.Models;

namespace AsusUefiImagePackEditor.UI.ViewModels;

public partial class ResourceItemViewModel: ObservableObject
{
    [ObservableProperty]
    private ResourceBlock _block;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SavePreviewCommand))]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    public bool _isModified;

    public string Id => Block.Id;
    public string DisplayName => $"Resource #{Index + 1} [{Id}]";
    public string SizeText => $"{Block.Data.Length} bytes";
    public bool CanSavePreview => PreviewImage is not null;
    public int Index { get; private set; }

    public ResourceItemViewModel(ResourceBlock block, int index)
    {
        _block = block;
        Index = index;
        LoadPreviewImage();
    }

    [RelayCommand]
    private async Task ReplaceResourceAsync()
    {
        OpenFileDialog dialog = new()
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string imagePath = dialog.FileName;

        try
        {
            using FileStream newStream = new(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            byte[] newData = new byte[newStream.Length];
            await newStream.ReadAsync(newData, 0, newData.Length);
            UpdateData(newData);

            MessageBox.Show("Resource replaced successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Replacement failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSavePreview))]
    private async Task SavePreviewAsync()
    {
        if (PreviewImage is null)
        {
            return;
        }

        SaveFileDialog dialog = new()
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap|*.bmp|All Files|*.*",
            FileName = $"Resource-{Index}-{Id}"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            string extension = Path.GetExtension(dialog.FileName).ToLower();

            await Task.Run(() =>
            {
                BitmapEncoder encoder = extension switch
                {
                    ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
                    ".bmp" => new BmpBitmapEncoder(),
                    _ => new PngBitmapEncoder(),
                };

                encoder.Frames.Add(BitmapFrame.Create(PreviewImage));

                using FileStream fs = new(dialog.FileName, FileMode.Create);
                encoder.Save(fs);
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save image: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateData(byte[] newData)
    {
        IsModified = true;
        Block.Data = newData;
        OnPropertyChanging(nameof(SizeText));
        OnPropertyChanged(nameof(SizeText));
        LoadPreviewImage();
    }

    private void LoadPreviewImage()
    {
        using MemoryStream dataStream = new(Block.Data);

        BitmapImage bitmapImage = new();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = dataStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        PreviewImage = bitmapImage;
    }
}
