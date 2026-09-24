using LwWinApp.Core;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace LwWinApp.Services;

/// <summary>文件选择与保存。</summary>
public interface IFilePickerService
{
    /// <summary>选单个文件，取消返回 null。</summary>
    Task<StorageFile?> PickFileAsync(string? commitButtonText = null, params string[] extensions);

    /// <summary>选多个文件，取消返回空列表。</summary>
    Task<IReadOnlyList<StorageFile>> PickFilesAsync(params string[] extensions);

    /// <summary>选一个保存位置，取消返回 null。返回的文件已经创建好。</summary>
    Task<StorageFile?> PickSaveFileAsync(
        string suggestedFileName,
        string? commitButtonText = null,
        params string[] extensions);

    /// <summary>选一个文件夹，取消返回 null。</summary>
    Task<StorageFolder?> PickFolderAsync(string? commitButtonText = null);
}

/// <summary>
/// 文件选择器。
///
/// 未打包应用没有包标识，WinRT 的 Picker 拿不到窗口上下文，
/// 必须先用 <c>InitializeWithWindow</c> 把主窗口句柄塞进去，
/// 否则调用会直接抛 <c>COMException</c>（0x80070005）。
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    private readonly IWindowContext _windowContext;

    public FilePickerService(IWindowContext windowContext)
    {
        _windowContext = windowContext;
    }

    public async Task<StorageFile?> PickFileAsync(string? commitButtonText = null, params string[] extensions)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };

        ApplyFilters(picker.FileTypeFilter, extensions);
        Initialize(picker);

        if (!string.IsNullOrWhiteSpace(commitButtonText))
            picker.CommitButtonText = commitButtonText;

        return await picker.PickSingleFileAsync();
    }

    public async Task<IReadOnlyList<StorageFile>> PickFilesAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };

        ApplyFilters(picker.FileTypeFilter, extensions);
        Initialize(picker);

        var files = await picker.PickMultipleFilesAsync();
        return files is null ? Array.Empty<StorageFile>() : files.ToList();
    }

    public async Task<StorageFile?> PickSaveFileAsync(
        string suggestedFileName,
        string? commitButtonText = null,
        params string[] extensions)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };

        // FileSavePicker 必须至少有一个类型选项，否则调用时抛异常。
        var (label, list) = NormalizeFilters(extensions);
        picker.FileTypeChoices.Add(label, list);

        Initialize(picker);

        if (!string.IsNullOrWhiteSpace(commitButtonText))
            picker.CommitButtonText = commitButtonText;

        return await picker.PickSaveFileAsync();
    }

    public async Task<StorageFolder?> PickFolderAsync(string? commitButtonText = null)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };

        // FolderPicker 也要求类型列表非空，给个通配即可。
        picker.FileTypeFilter.Add("*");

        Initialize(picker);

        if (!string.IsNullOrWhiteSpace(commitButtonText))
            picker.CommitButtonText = commitButtonText;

        return await picker.PickSingleFolderAsync();
    }

    // ---------------------------------------------------------------

    /// <summary>把窗口句柄交给 Picker。这是未打包应用能弹出选择框的前提。</summary>
    private void Initialize(object picker)
    {
        var handle = _windowContext.Handle;

        if (handle == IntPtr.Zero)
            throw new BusinessException("窗口尚未就绪，暂时无法打开文件选择框。");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, handle);
    }

    private static void ApplyFilters(IList<string> target, string[] extensions)
    {
        var (_, list) = NormalizeFilters(extensions);

        foreach (var extension in list)
            target.Add(extension);
    }

    /// <summary>
    /// 归一化扩展名：统一补上点号、去掉重复。传空则给一组常用类型，
    /// 因为 Picker 的空过滤器列表会导致调用失败。
    /// </summary>
    private static (string Label, List<string> Extensions) NormalizeFilters(string[] extensions)
    {
        if (extensions.Length == 0)
            return ("所有文件", new List<string> { "*" });

        var list = new List<string>();

        foreach (var raw in extensions)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            // 调用方可能传 "csv" 也可能传 ".csv"，cvs 需要的是带点号的形式。
            var extension = raw.StartsWith('.') ? raw : "." + raw;

            if (!list.Contains(extension, StringComparer.OrdinalIgnoreCase))
                list.Add(extension);
        }

        if (list.Count == 0)
            list.Add("*");

        var label = string.Join(" / ", list.Select(e => e.TrimStart('.').ToUpperInvariant()));

        return (label, list);
    }
}
