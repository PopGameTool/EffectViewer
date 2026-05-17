using EffectViewer.Projects;
using EffectViewer.Tests.TestUtilities;
using EffectViewer.ViewModels;

namespace EffectViewer.Tests.ViewModels;

public sealed class MainViewModelResourceWorkflowTests
{
    private static readonly byte[] PngHeader = [137, 80, 78, 71, 13, 10, 26, 10];

    [Fact]
    public async Task CreateResourceKeepsDirtyCurrentResourceOpen()
    {
        using TempDirectory temp = new();
        MainViewModel viewModel = await CreateProjectViewModelAsync(temp);
        ShowcaseEditorViewModel dirtyEditor = await CreateDirtyShowcaseEditorAsync(viewModel, "intro");
        string unsavedScript = dirtyEditor.ScriptText;

        viewModel.NewResourceKind = EffectAssetKind.Showcase;
        viewModel.NewResourceId = "outro";

        await AssertCompletesWithoutUnsavedPromptAsync(
            viewModel.CreateResourceCommand.ExecuteAsync(null),
            viewModel);

        Assert.True(dirtyEditor.IsDirty);
        Assert.Equal(unsavedScript, dirtyEditor.ScriptText);
        Assert.Contains(viewModel.OpenEditors, editor => ReferenceEquals(editor, dirtyEditor));
        Assert.Contains(
            viewModel.OpenEditors.OfType<ShowcaseEditorViewModel>(),
            editor => editor.AssetId == "outro");
    }

    [Fact]
    public async Task ImportResourceFileKeepsDirtyCurrentResourceOpen()
    {
        using TempDirectory temp = new();
        MainViewModel viewModel = await CreateProjectViewModelAsync(temp);
        ShowcaseEditorViewModel dirtyEditor = await CreateDirtyShowcaseEditorAsync(viewModel, "intro");
        string unsavedScript = dirtyEditor.ScriptText;

        await using MemoryStream stream = new(PngHeader);
        await AssertCompletesWithoutUnsavedPromptAsync(
            viewModel.ImportResourceFileAsync("New Image.PNG", stream),
            viewModel);

        Assert.True(dirtyEditor.IsDirty);
        Assert.Equal(unsavedScript, dirtyEditor.ScriptText);
        Assert.Contains(viewModel.OpenEditors, editor => ReferenceEquals(editor, dirtyEditor));
        Assert.Contains(
            viewModel.OpenEditors.OfType<ImageEditorViewModel>(),
            editor => editor.AssetId == "IMAGE_NEW_IMAGE");
    }

    private static async Task<MainViewModel> CreateProjectViewModelAsync(TempDirectory temp)
    {
        MainViewModel viewModel = new(new TestProjectStorageProvider(temp.Path));
        viewModel.NewProjectName = "Resource Workflow";

        await viewModel.CreateProjectCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.CurrentProject);
        Assert.False(viewModel.IsUnsavedChangesPromptOpen);
        return viewModel;
    }

    private static async Task<ShowcaseEditorViewModel> CreateDirtyShowcaseEditorAsync(
        MainViewModel viewModel,
        string assetId)
    {
        viewModel.NewResourceKind = EffectAssetKind.Showcase;
        viewModel.NewResourceId = assetId;

        await viewModel.CreateResourceCommand.ExecuteAsync(null);

        ShowcaseEditorViewModel editor = Assert.IsType<ShowcaseEditorViewModel>(viewModel.SelectedEditor);
        editor.ScriptText += $"{Environment.NewLine}-- unsaved edit";

        Assert.True(editor.IsDirty);
        return editor;
    }

    private static async Task AssertCompletesWithoutUnsavedPromptAsync(Task task, MainViewModel viewModel)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(task, completed);
        await task;
        Assert.False(viewModel.IsUnsavedChangesPromptOpen);
    }
}
