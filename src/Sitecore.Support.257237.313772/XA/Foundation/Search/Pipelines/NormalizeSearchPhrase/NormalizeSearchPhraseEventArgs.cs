namespace Sitecore.Support.XA.Foundation.Search.Pipelines.NormalizeSearchPhrase
{

  using System;
  using System.Runtime.Serialization;
  using System.Security.Permissions;
  using Sitecore.Pipelines;

  [Serializable]
  public class NormalizeSearchPhraseEventArgs : PipelineArgs
  {
    public string Phrase;
    public NormalizeSearchPhraseEventArgs()
    {
    }
    protected NormalizeSearchPhraseEventArgs(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
    [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      if (info == null)
      {
        throw new ArgumentNullException(nameof(info));
      }
      base.GetObjectData(info, context);
      info.AddValue("Phrase", Phrase);
    }
  }
}