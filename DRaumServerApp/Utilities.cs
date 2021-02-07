using System;
using System.Collections.Generic;
using System.Text;

namespace DRaumServerApp
{
  class Utilities
  {
    private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static bool nextLevelCalculated = false;
    private static long[] nextLevelExp;

    public static bool RUNNINGINTESTMODE = false;

    internal static long getNextLevelExp(int actualLevel)
    {
      if(!Utilities.nextLevelCalculated)
      {
        long expsum = 0;
        Utilities.nextLevelExp = new long[500];
        for( int i = 0 ; i < 500 ; i++ )
        {
          Utilities.nextLevelExp[i] = (long)Math.Round(5 + 0.25 * (i ^ 3) + 0.5 * (i ^ 2) + 7 * i);
          
          expsum += Utilities.nextLevelExp[i];
          if (i%10==0)
          {
            logger.Info("lvl " + i + " : " + Utilities.nextLevelExp[i] + " , " + expsum);
          }
          
        }
        Utilities.nextLevelCalculated = true;
      }
      if(actualLevel < 500)
      {
        return Utilities.nextLevelExp[actualLevel];
      }
      else
      {
        return long.MaxValue;
      }
    }

  }
}
