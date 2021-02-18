using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
