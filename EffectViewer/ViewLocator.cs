using Avalonia.Controls;
using Avalonia.Controls.Templates;
using EffectViewer.ViewModels;
using EffectViewer.Views;

namespace EffectViewer
{
    /// <summary>
    /// Given a view model, returns the corresponding view if possible.
    /// </summary>
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object param)
        {
            return param switch
            {
                MainViewModel => new MainView(),
                EffectEditorViewModel => new EffectEditorView(),
                ImageEditorViewModel => new ImageEditorView(),
                ShowcaseEditorViewModel => new ShowcaseEditorView(),
                WelcomeEditorViewModel => new WelcomeEditorView(),
                null => null,
                _ => new TextBlock { Text = "Not Found: " + param.GetType().FullName }
            };
        }

        public bool Match(object data)
        {
            return data is MainViewModel
                or EffectEditorViewModel
                or ImageEditorViewModel
                or ShowcaseEditorViewModel
                or WelcomeEditorViewModel;
        }
    }
}
