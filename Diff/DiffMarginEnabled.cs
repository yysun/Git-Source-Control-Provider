namespace GitScc.Diff
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(EditorOptionDefinition))]
    [Name(GitTextViewOptions.DiffMarginName)]
    public sealed class DiffMarginEnabled : ViewOptionDefinition<bool>
    {
        public override bool Default
        {
            get
            {
                return true;
            }
        }

        public override EditorOptionKey<bool> Key
        {
            get
            {
                return GitTextViewOptions.DiffMarginId;
            }
        }

        [Export(typeof(IWpfTextViewCreationListener))]
        [ContentType("text")]
        [TextViewRole(PredefinedTextViewRoles.Document)]
        private class TextViewListener : IWpfTextViewCreationListener
        {
            public void TextViewCreated(IWpfTextView textView)
            {
                if (textView == null)
                    return;

                textView.Options.SetOptionValue(GitTextViewOptions.DiffMarginId, !GitSccOptions.Current.DisableDiffMargin);
            }
        }
    }
}
