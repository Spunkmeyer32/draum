using System;
using System.Collections.Generic;
using System.Text;

namespace DRaumServerApp
{
  internal class PostingPublishManager
  {
    private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static int firstHour = 9; // 9
    private static int lastHour = 20; // 20
    private static int minutesBetween = 20; // 20

    private static int premiumHour = 17;
    private static int happyHour = premiumHour+1;

    private DateTime nextPublishSlot = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);

    internal PostingPublishManager()
    {
      if (Utilities.RUNNINGINTESTMODE)
      {
        firstHour = 0;
        lastHour = 23;
        minutesBetween = 2;
      }
    }


    internal void calcNextSlot()
    {
      DateTime now = DateTime.Now;
      while(this.nextPublishSlot <= now)
      {
        this.nextPublishSlot = this.nextPublishSlot.AddMinutes(minutesBetween);        
        if(this.nextPublishSlot.Hour > lastHour)
        {
          logger.Info("Zu spät, springe zum nächsten Tag: " + this.nextPublishSlot.ToString());
          this.nextPublishSlot = this.nextPublishSlot.AddDays(1);
          this.nextPublishSlot = new DateTime(this.nextPublishSlot.Year, this.nextPublishSlot.Month, this.nextPublishSlot.Day, firstHour, 0, 0);
        }
        if (this.nextPublishSlot.Hour < firstHour)
        {
          logger.Info("Zu früh, springe zur ersten Stunde");
          this.nextPublishSlot = new DateTime(this.nextPublishSlot.Year, this.nextPublishSlot.Month, this.nextPublishSlot.Day, firstHour, 0, 0);
        }
      }
      logger.Debug("Neuer Slot: " + this.nextPublishSlot.ToString());
    }

    public enum publishHourType { UNKNOWN, PREMIUM, HAPPY, NORMAL, NONE };

    internal DateTime getTimestampOfNextSlot(int listsize, publishHourType publishType)
    {
      DateTime now = DateTime.Now;
      DateTime nextslot;
      if (publishType.Equals(publishHourType.PREMIUM))
      {
        if (now.Hour == premiumHour)
        {
          // Mittendrin, also losgehts
          nextslot = this.nextPublishSlot;
        }
        else
        {
          if (now.Hour > premiumHour)
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, premiumHour, 0, 0).AddDays(1);
          }
          else
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, premiumHour, 0, 0);
          }
        }
        for (int i = 0; i < listsize; i++)
        {
          nextslot = nextslot.AddMinutes(minutesBetween);
          if (nextslot.Hour > premiumHour)
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, premiumHour, 0, 0).AddDays(1);
          }
        }
        return nextslot;
      }
      if (publishType.Equals(publishHourType.HAPPY))
      {
        if (now.Hour == happyHour)
        {
          // Mittendrin, also losgehts
          nextslot = this.nextPublishSlot;
        }
        else
        {
          if (now.Hour > happyHour)
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, happyHour, 0, 0).AddDays(1);
          }
          else
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, happyHour, 0, 0);
          }
        }
        for (int i = 0; i < listsize; i++)
        {
          nextslot = nextslot.AddMinutes(minutesBetween);
          if (nextslot.Hour > happyHour)
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, happyHour, 0, 0).AddDays(1);
          }
        }
        return nextslot;
      }
      if (now.Hour != happyHour && now.Hour != premiumHour)
      {
        // Mittendrin, also losgehts
        nextslot = this.nextPublishSlot;
      }
      else
      {
        // nach den hours beginnen
        nextslot = new DateTime(now.Year, now.Month, now.Day, happyHour+1, 0, 0);
      }
      for (int i = 0; i < listsize; i++)
      {
        nextslot = nextslot.AddMinutes(minutesBetween);
        if (nextslot.Hour > lastHour)
        {
          // nächster tag
          nextslot = new DateTime(now.Year, now.Month, now.Day, firstHour, 0, 0).AddDays(1);
        }
        if (nextslot.Hour >= premiumHour)
        {
          // nach den hours fortfahren
          nextslot = new DateTime(now.Year, now.Month, now.Day, happyHour+1, 0, 0);
        }
      }
      return nextslot;
    }

    internal publishHourType getCurrentpublishType()
    {      
      if(DateTime.Now > this.nextPublishSlot)
      {
        publishHourType result = publishHourType.NONE;
        if (this.nextPublishSlot.Hour == premiumHour)
        {
          result = publishHourType.PREMIUM;
        }
        else
        {
          if (this.nextPublishSlot.Hour == happyHour)
          {
            result = publishHourType.HAPPY;
          }
          else
          {
            result = publishHourType.NORMAL;
          }
        }

        this.calcNextSlot();
        return result;
      }
      return publishHourType.NONE;
    }


  }
}
