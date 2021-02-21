using System;
using System.Text.RegularExpressions;

namespace DRaumServerApp.Postings
{
  internal static class SpamFilter
  {
    private static readonly int minLen = 100;
    private static readonly int maxLen = 1500;
    private static readonly Regex linkParser = new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] badwords = { 
      "nazischweine" ,
      "linke zecken",
      "arschloch",
      "nigger",
      "neger",
      "judenschwein",
      "nutte",
      "sieg heil",
      "heil hitler",
      "mit deutschem Gruß",
      "mit deutschem Gruss",
      "meine ehre heißt treue",
      "meine ehre heisst treue"
    };
    private static readonly string[] goodwords = {
      "nazis",
      "linke",
      "ars****ch",
      "schwarzer",
      "schwarzer",
      "jude",
      "prostituierte",
      "***",
      "***",
      "mit freundlichen grüßen",
      "mit freundlichen grüßen",
      "treue ist mir wichtig",
      "treue ist mir wichtig"
    };


    public static bool checkPostInput(string input, out string output, out string message)
    {
      // CheckLength
      int tlen = input.Length;
      if(tlen < minLen)
      {
        message = "Text ist zu kurz. Mindestens " + minLen + " Zeichen.";
        output = new string(input);
        return false;
      }
      if(tlen > maxLen)
      {
        message = "Text ist zu lang. Maximal " + maxLen + " Zeichen.";
        output = new string(input);
        return false;
      }
      // Filter commands
      if(input.StartsWith('/'))
      {
        message = "Text darf nicht mit / beginnen.";
        output = new string(input);
        return false;
      }
      // Filter URLs      
      if(linkParser.IsMatch(input))
      {
        message = "Text scheint URLs zu beinhalten (http, www, etc..) ";
        output = new string(input);
        return false;
      }
      message = "OK";
      bool wordflag = false;
      for(int i=0;i< badwords.Length;i++)
      {
        if(input.Contains(badwords[i], StringComparison.CurrentCultureIgnoreCase))
        {
          input = input.Replace(badwords[i], goodwords[i], StringComparison.CurrentCultureIgnoreCase);
          wordflag = true;
        }
      }
      if(wordflag)
      {
        message = "OK, es wurden Wörter ersetzt";
      }
      
      

      output = new string(input);      
      return true;
    }

    

  }
}
