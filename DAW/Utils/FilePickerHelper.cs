using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace DAW.Utils;

internal static class FilePickerHelper
{
    public static async Task<StorageFile> ShowOpenPickerAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        return await picker.PickSingleFileAsync();
    }

    public static async Task<StorageFile> ShowSavePickerAsync(string suggestedFileName)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeChoices.Add("Audio File", [".wav"]);
        picker.SuggestedFileName = suggestedFileName;

        return await picker.PickSaveFileAsync();
    }
}
