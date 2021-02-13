using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DRaumServerApp
{
  class Utilities
  {
    private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static bool nextLevelCalculated = false;
    private static long[] nextLevelExp;

    public static bool RUNNINGINTESTMODE = true;

    public static CultureInfo usedCultureInfo = new CultureInfo("de-DE", false);

    internal static long getNextLevelExp(int actualLevel)
    {
      if(!nextLevelCalculated)
      {
        long expsum = 0;
        nextLevelExp = new long[500];
        for( int i = 0 ; i < 500 ; i++ )
        {
          nextLevelExp[i] = (long)Math.Round(5 + 0.25 * (i ^ 3) + 0.5 * (i ^ 2) + 7 * i);
          
          expsum += nextLevelExp[i];
          if (i%10==0)
          {
            logger.Info("lvl " + i + " : " + nextLevelExp[i] + " , " + expsum);
          }
          
        }
        nextLevelCalculated = true;
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
