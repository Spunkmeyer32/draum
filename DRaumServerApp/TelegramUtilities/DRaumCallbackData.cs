namespace DRaumServerApp.TelegramUtilities
{
  internal class DRaumCallbackData
  {
    private readonly string callbackPrefix;
    private readonly long id;

    private DRaumCallbackData(string prefix, long id)
    {
      this.callbackPrefix = prefix;
      this.id = id;
    }

    internal string getPrefix()
    {
      return this.callbackPrefix;
    }

    internal long getId()
    {
      return this.id;
    }

    internal static DRaumCallbackData parseCallbackData(string callbackdatastring)
    {
      string callbackAction = callbackdatastring.Substring(0, 1);
      string referencedIdString = callbackdatastring.Substring(1);
      long referencedId = 0;
      if (referencedIdString.Trim().Length > 0)
      {
        referencedId = long.Parse(referencedIdString);
      }
      return new DRaumCallbackData(callbackAction, referencedId);
    }


  }
}