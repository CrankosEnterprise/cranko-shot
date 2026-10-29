using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ShotMind.Core.Models;
using ShotMind.Core.Services;

namespace ShotMind.UI.Views;

public class HistoryItemViewModel
{
    public HistoryItem Source { get; }
    public BitmapSource? ThumbnailSource { get; }

    public string FileName => Source.FileName;
    public string FilePath => Source.FilePath;
    public string Format => Source.Format;
    public string TimestampFormatted => Source.Timestamp.ToString("dd/MM/yyyy HH:mm");
    public string DimensionsFormatted => $"{Source.Width} × {Source.Height} px";

    public HistoryItemViewModel(HistoryItem item)
    {
        Source = item;

        if (!string.IsNullOrEmpty(item.ThumbnailBase64))
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(item.ThumbnailBase64);
                using var ms = new MemoryStream(bytes);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                ThumbnailSource = bi;
            }
            catch { }
        }
    }
}

public partial class HistoryWindow : Window
{
    private readonly ObservableCollection<HistoryItemViewModel> _items = new();

    public HistoryWindow()
    {
        InitializeComponent();
        CapturesListBox.ItemsSource = _items;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCapturesAsync();
    }

    private async Task LoadCapturesAsync(string? search = null)
    {
        _items.Clear();
        var captures = await HistoryService.Instance.GetCapturesAsync(search);
        foreach (var c in captures)
        {
            _items.Add(new HistoryItemViewModel(c));
        }

        ItemsCountText.Text = $"{_items.Count} captura(s) no histórico";
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string text = SearchBox.Text.Trim();
        await LoadCapturesAsync(string.IsNullOrEmpty(text) ? null : text);
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadCapturesAsync(SearchBox.Text.Trim());
    }

    private void CapturesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void BtnItemEditor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is HistoryItemViewModel vm)
        {
            if (File.Exists(vm.FilePath))
            {
                try
                {
                    using var bmp = new Bitmap(vm.FilePath);
                    var editor = new FullEditorWindow();
                    editor.LoadBitmap(bmp, vm.FilePath);
                    editor.Show();
                    editor.Activate();
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao abrir imagem no editor: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("O arquivo original não foi encontrado no disco (pode ter sido movido ou excluído).", "Arquivo Não Encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnItemCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is HistoryItemViewModel vm)
        {
            if (File.Exists(vm.FilePath))
            {
                try
                {
                    using var bmp = new Bitmap(vm.FilePath);
                    ExportService.CopyToClipboard(bmp);
                    MessageBox.Show("Imagem copiada para a área de transferência!", "CrankoShot", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao copiar imagem: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("O arquivo original não foi encontrado no disco.", "Arquivo Não Encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnItemOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is HistoryItemViewModel vm)
        {
            if (File.Exists(vm.FilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{vm.FilePath}\"");
            }
            else if (!string.IsNullOrEmpty(vm.FilePath))
            {
                string? dir = Path.GetDirectoryName(vm.FilePath);
                if (Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", $"\"{dir}\"");
                }
                else
                {
                    MessageBox.Show("A pasta de destino não foi localizada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }

    private async void BtnItemDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is HistoryItemViewModel vm)
        {
            var res = MessageBox.Show($"Deseja remover \"{vm.FileName}\" do histórico?", "Excluir Registro", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                await HistoryService.Instance.DeleteCaptureAsync(vm.Source.Id);
                _items.Remove(vm);
                ItemsCountText.Text = $"{_items.Count} captura(s) no histórico";
            }
        }
    }

    private async void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show(
            "Tem certeza de que deseja limpar o histórico de capturas?\n\n(Os arquivos salvos no disco NÃO serão apagados.)",
            "Limpar Histórico",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (res == MessageBoxResult.Yes)
        {
            await HistoryService.Instance.ClearHistoryAsync(false);
            _items.Clear();
            ItemsCountText.Text = "0 capturas no histórico";
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
