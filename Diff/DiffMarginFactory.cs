namespace GitScc.Diff
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(DiffMargin.MarginName)]
    [Order(Before = PredefinedMarginNames.LineNumber)]
    [MarginContainer(PredefinedMarginNames.LeftSelection)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class DiffMarginFactory : IWpfTextViewMarginProvider
    {
        [Import]
        private ITextDocumentFactoryService TextDocumentFactoryService
        {
            get;
            set;
        }

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
        {
            return new DiffMargin(textViewHost.TextView, TextDocumentFactoryService);
        }
    }
}
