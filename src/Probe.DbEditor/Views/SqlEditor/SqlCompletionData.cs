using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Probe.DbEditor.Views.SqlEditor;

public sealed class SqlCompletionData : ICompletionData
{
    public SqlCompletionData(string text, string kind, double priority)
    {
        Text = text;
        Content = $"{text}  {kind}";
        Description = kind;
        Priority = priority;
    }

    public ImageSource? Image => null;
    public string Text { get; }
    public object Content { get; }
    public object Description { get; }
    public double Priority { get; }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}
