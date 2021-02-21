using System;
using System.Globalization;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DRaumServerApp
{
  internal static class Utilities
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static bool _nextLevelCalculated;
    private static long[] _nextLevelExp;

    public static bool Runningintestmode = false;

    public static readonly CultureInfo UsedCultureInfo = CultureInfo.CreateSpecificCulture("de-DE");

    internal static string getHumanAbbrevNumber(long number)
    {
      if (number >= 1000000)
      {
        return (number / 1000000.0).ToString("0.0") + " Mio";
      }
      if (number >= 1000)
      {
        return (number / 1000.0).ToString("0.0") + " Tsd";
      }
      return number.ToString();
    }

    private const int Maxinsertchars = 50;
    private static readonly char[] temp = new char[Maxinsertchars];

    private static void insert(ref char[][] strarray, ref int[] startposarray, int targetChar, string insert)
    {
      int i;
      int inslen = insert.Length;
      int k = 0;
      for (i = startposarray[targetChar]; i < Maxinsertchars; i++)
      {
        if (strarray[targetChar][i] == '\0')
        {
          temp[k] = '\0';
          break;
        }

        temp[k] = strarray[targetChar][i];
        k++;
      }
      for (i = 0; i < inslen; i++)
      {
        strarray[targetChar][startposarray[targetChar]+i] = insert[i];
      }
      for (i = 0; i < Maxinsertchars-(inslen+startposarray[targetChar]); i++)
      {
        if (temp[i] == '\0')
        {
          strarray[targetChar][startposarray[targetChar] + inslen + i] = '\0';
          startposarray[targetChar] = (inslen+i) - 1;
          break;
        }
        strarray[targetChar][startposarray[targetChar] + inslen + i] = temp[i];
      }
    }

    internal static string telegramEntitiesToHtml(string text, MessageEntity[] entities)
    {
      char[][] strarray = new char[text.Length][];
      int[] startposarray = new int[text.Length];
      for (int i = 0; i < text.Length; i++)
      {
        startposarray[i] = 0;
        int replacementLength = 1;
        strarray[i] = new char[Maxinsertchars];
        if (text[i] == '&')
        {
          strarray[i][0] = '&';
          strarray[i][1] = 'a';
          strarray[i][2] = 'm';
          strarray[i][3] = 'p';
          strarray[i][4] = ';';
          replacementLength = 5;
        }
        if (text[i] == '<')
        {
          strarray[i][0] = '&';
          strarray[i][1] = 'l';
          strarray[i][2] = 't';
          strarray[i][3] = ';';
          replacementLength = 4;
        }
        if (text[i] == '>')
        {
          strarray[i][0] = '&';
          strarray[i][1] = 'g';
          strarray[i][2] = 't';
          strarray[i][3] = ';';
          replacementLength = 4;
        }
        if (text[i] == '\"')
        {
          strarray[i][0] = '&';
          strarray[i][1] = 'q';
          strarray[i][2] = 'u';
          strarray[i][3] = 'o';
          strarray[i][4] = 't';
          strarray[i][5] = ';';
          replacementLength = 6;
        }
        if (replacementLength == 1)
        {
          strarray[i][0] = text[i];
        }
        strarray[i][replacementLength] = '\0';
      }

      if (entities != null)
      {
        foreach (MessageEntity entity in entities)
        {
          switch (entity.Type)
          {
            case MessageEntityType.Bold:
              insert(ref strarray, ref startposarray, entity.Offset, "<b>");
              insert(ref strarray, ref startposarray, entity.Offset + entity.Length, "</b>");
              break;
            case MessageEntityType.Italic:
              insert(ref strarray, ref startposarray, entity.Offset, "<i>");
              insert(ref strarray, ref startposarray, entity.Offset + entity.Length, "</i>");
              break;
            case MessageEntityType.Underline:
              insert(ref strarray, ref startposarray, entity.Offset, "<u>");
              insert(ref strarray, ref startposarray, entity.Offset + entity.Length, "</u>");
              break;
            case MessageEntityType.Strikethrough:
              insert(ref strarray, ref startposarray, entity.Offset, "<s>");
              insert(ref strarray, ref startposarray, entity.Offset + entity.Length, "</s>");
              break;
            case MessageEntityType.Code:
              insert(ref strarray, ref startposarray, entity.Offset, "<code>");
              insert(ref strarray, ref startposarray, entity.Offset + entity.Length, "</code>");
              break;
          }
        }
      }

      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < text.Length; i++)
      {
        for (int k = 0; k < Maxinsertchars; k++)
        {
          if (strarray[i][k] != '\0')
          {
            sb.Append(strarray[i][k]);
          }
        }
      }
      return sb.ToString();
    }

    internal static long getNextLevelExp(int actualLevel)
    {
      if(!_nextLevelCalculated)
      {
        long expsum = 0;
        _nextLevelExp = new long[500];
        for( int i = 0 ; i < 500 ; i++ )
        {
          _nextLevelExp[i] = expsum + (long) Math.Round(8 + 0.25 * (i ^ 3) + 0.6 * (i ^ 2) + 8 * i);
          expsum = _nextLevelExp[i];
          if (i%10==0)
          {
            logger.Info("lvl " + i + " : " + _nextLevelExp[i] + " , " + expsum);
          }
        }

        _nextLevelCalculated = true;
      }
      if (actualLevel <= 0)
      {
        return 0;
      }
      if(actualLevel < 500)
      {
        return _nextLevelExp[actualLevel];
      }
      else
      {
        return long.MaxValue;
      }
    }

  }
}
