namespace Sitecore.Support.XA.Foundation.Search.Pipelines.NormalizeSearchPhrase
{
  using System.Collections.Generic;

  public class RemoveSpecialSearchCharactersProcessor : NormalizeSearchPhraseProcessor
  {
    public override void Process(NormalizeSearchPhraseEventArgs args)
    {
      if (string.IsNullOrWhiteSpace(args.Phrase))
      {
        return;
      }
      foreach (string escapeCharacter in GetEscapeCharacterSet())
      {
        args.Phrase = args.Phrase.Replace(escapeCharacter, " ");
      }
      if (string.IsNullOrWhiteSpace(args.Phrase))
      {
        args.Phrase = string.Empty;
      }
    }
    protected virtual HashSet<string> GetEscapeCharacterSet()
    {
      return new HashSet<string> { "+", "-", "&", "|", "!", "{", "}", "[", "]", "^", "(", ")", "~", ":", ";", "/", @"\", "?", @"""" };
    }
  }
}