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
    }
}
