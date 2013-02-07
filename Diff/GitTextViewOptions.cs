namespace GitScc.Diff
{
    using Microsoft.VisualStudio.Text.Editor;

    public static class GitTextViewOptions
    {
        public const string DiffMarginName = "GitScc/DiffMarginName";

        public static readonly EditorOptionKey<bool> DiffMarginId = new EditorOptionKey<bool>(DiffMarginName);
    }
}
