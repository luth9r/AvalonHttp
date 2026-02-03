using System.Collections.Generic;
using System.Linq;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace AvalonHttp.Common.FoldingStrategies;

public class BraceFoldingStrategy
{
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        int firstErrorOffset;
        var newFoldings = CreateNewFoldings(document, out firstErrorOffset);
        manager.UpdateFoldings(newFoldings, firstErrorOffset);
    }

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var list = new List<NewFolding>();
        var openingBraces = new Stack<int>();

        for (int i = 0; i < document.TextLength; i++)
        {
            char c = document.GetCharAt(i);
            if (c == '{' || c == '[')
            {
                openingBraces.Push(i);
            }
            else if ((c == '}' || c == ']') && openingBraces.Count > 0)
            {
                int start = openingBraces.Pop();
                if (start < i)
                {
                    list.Add(new NewFolding(start, i + 1));
                }
            }
        }
        return list.OrderBy(f => f.StartOffset);
    }
}