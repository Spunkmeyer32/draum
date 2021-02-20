using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DRaumServerApp
{
  internal class Utilities
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static bool nextLevelCalculated = false;
    private static long[] nextLevelExp;

    public static bool RUNNINGINTESTMODE = false;

    public static CultureInfo usedCultureInfo = CultureInfo.CreateSpecificCulture("de-DE");

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
    private static char[] temp = new char[Maxinsertchars];

    private static void insert(ref char[][] strarray, int targetChar, string insert)
    {
      int i;
      int inslen = insert.Length;
      for (i = 0; i < Maxinsertchars; i++)
      {
        if (strarray[targetChar][i] == '\0')
        {
          temp[i] = '\0';
          break;
        }
        temp[i] = strarray[targetChar][i];
      }
      for (i = 0; i < inslen; i++)
      {
        strarray[targetChar][i] = insert[i];
      }
      for (i = 0; i < Maxinsertchars-inslen; i++)
      {
        if (temp[i] == '\0')
        {
          strarray[targetChar][inslen + i] = '\0';
          break;
        }
        strarray[targetChar][inslen + i] = temp[i];
      }
    }

    internal static string telegramEntitiesToHtml(string text, MessageEntity[] entities)
    {
      char[][] strarray = new char[text.Length][];
      for (int i = 0; i < text.Length; i++)
      {
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
      foreach (MessageEntity entity in entities)
      {
        switch (entity.Type)
        {
          case MessageEntityType.Bold:
            insert(ref strarray, entity.Offset, "<b>");
            insert(ref strarray, entity.Offset+entity.Length, "</b>");
            break;
          case MessageEntityType.Italic:
            insert(ref strarray, entity.Offset, "<i>");
            insert(ref strarray, entity.Offset+entity.Length, "</i>");
            break;
          case MessageEntityType.Underline:
            insert(ref strarray, entity.Offset, "<u>");
            insert(ref strarray, entity.Offset+entity.Length, "</u>");
            break;
          case MessageEntityType.Strikethrough:
            insert(ref strarray, entity.Offset, "<s>");
            insert(ref strarray, entity.Offset+entity.Length, "</s>");
            break;
          case MessageEntityType.Code:
            insert(ref strarray, entity.Offset, "<code>");
            insert(ref strarray, entity.Offset+entity.Length, "</code>");
            break;
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
      if(!nextLevelCalculated)
      {
        long expsum = 0;
        nextLevelExp = new long[500];
        for( int i = 0 ; i < 500 ; i++ )
        {
          nextLevelExp[i] = expsum + (long) Math.Round(8 + 0.25 * (i ^ 3) + 0.6 * (i ^ 2) + 8 * i);
          expsum = nextLevelExp[i];
          if (i%10==0)
          {
            logger.Info("lvl " + i + " : " + nextLevelExp[i] + " , " + expsum);
          }
        }
        nextLevelCalculated = true;
      }
      if (actualLevel <= 0)
      {
        return 0;
      }
      if(actualLevel < 500)
      {
        return nextLevelExp[actualLevel];
      }
      else
      {
        return long.MaxValue;
      }
    }

  }
}
