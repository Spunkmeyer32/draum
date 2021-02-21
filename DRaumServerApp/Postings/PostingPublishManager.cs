using System;

namespace DRaumServerApp.Postings
{
  internal class PostingPublishManager
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private static int _firstHour = 9; // 9
    private static int _lastHour = 20; // 20
    private static int _minutesBetween = 20; // 20

    private const int PremiumHour = 17;
    private const int HappyHour = PremiumHour + 1;

    private DateTime nextPublishSlot = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);

    internal PostingPublishManager()
    {
      if (Utilities.Runningintestmode)
      {
        _firstHour = 0;
        _lastHour = 23;
        _minutesBetween = 2;
      }
    }


    internal void calcNextSlot()
    {
      DateTime now = DateTime.Now;
      while(this.nextPublishSlot <= now)
      {
        this.nextPublishSlot = this.nextPublishSlot.AddMinutes(_minutesBetween);        
        if(this.nextPublishSlot.Hour > _lastHour)
        {
          logger.Info("Zu spät, springe zum nächsten Tag: " + this.nextPublishSlot.ToString(Utilities.UsedCultureInfo));
          this.nextPublishSlot = this.nextPublishSlot.AddDays(1);
          this.nextPublishSlot = new DateTime(this.nextPublishSlot.Year, this.nextPublishSlot.Month, this.nextPublishSlot.Day, _firstHour, 0, 0);
        }
        if (this.nextPublishSlot.Hour < _firstHour)
        {
          logger.Info("Zu früh, springe zur ersten Stunde");
          this.nextPublishSlot = new DateTime(this.nextPublishSlot.Year, this.nextPublishSlot.Month, this.nextPublishSlot.Day, _firstHour, 0, 0);
        }
      }

      logger.Info("Neuer Slot: " + this.nextPublishSlot.ToString(Utilities.UsedCultureInfo));
    }

    public enum PublishHourType { Premium, Happy, Normal, None };

    internal DateTime getTimestampOfNextSlot(int listsize, PublishHourType publishType)
    {
      DateTime now = DateTime.Now;
      DateTime nextslot;
      if (publishType.Equals(PublishHourType.Premium))
      {
        if (now.Hour == PremiumHour)
        {
          // Mittendrin, also losgehts
          nextslot = this.nextPublishSlot;
        }
        else
        {
          if (now.Hour > PremiumHour)
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, PremiumHour, 0, 0).AddDays(1);
          }
          else
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, PremiumHour, 0, 0);
          }
        }
        for (int i = 0; i < listsize; i++)
        {
          nextslot = nextslot.AddMinutes(_minutesBetween);
          if (nextslot.Hour > PremiumHour)
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, PremiumHour, 0, 0).AddDays(1);
          }
        }
        return nextslot;
      }
      if (publishType.Equals(PublishHourType.Happy))
      {
        if (now.Hour == HappyHour)
        {
          // Mittendrin, also losgehts
          nextslot = this.nextPublishSlot;
        }
        else
        {
          if (now.Hour > HappyHour)
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, HappyHour, 0, 0).AddDays(1);
          }
          else
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, HappyHour, 0, 0);
          }
        }
        for (int i = 0; i < listsize; i++)
        {
          nextslot = nextslot.AddMinutes(_minutesBetween);
          if (nextslot.Hour > HappyHour)
          {
            nextslot = new DateTime(now.Year, now.Month, now.Day, HappyHour, 0, 0).AddDays(1);
          }
        }
        return nextslot;
      }
      if (now.Hour != HappyHour && now.Hour != PremiumHour)
      {
        // Mittendrin, also losgehts
        nextslot = this.nextPublishSlot;
      }
      else
      {
        // nach den hours beginnen
        nextslot = new DateTime(now.Year, now.Month, now.Day, HappyHour+1, 0, 0);
      }
      for (int i = 0; i < listsize; i++)
      {
        nextslot = nextslot.AddMinutes(_minutesBetween);
        if (nextslot.Hour > _lastHour)
        {
          // nächster tag
          nextslot = new DateTime(now.Year, now.Month, now.Day, _firstHour, 0, 0).AddDays(1);
        }
        if (nextslot.Hour >= PremiumHour)
        {
          // nach den hours fortfahren
          nextslot = new DateTime(now.Year, now.Month, now.Day, HappyHour+1, 0, 0);
        }
      }
      return nextslot;
    }

    internal PublishHourType getCurrentpublishType()
    {      
      if(DateTime.Now > this.nextPublishSlot)
      {
        PublishHourType result;
        if (this.nextPublishSlot.Hour == PremiumHour)
        {
          result = PublishHourType.Premium;
        }
        else
        {
          if (this.nextPublishSlot.Hour == HappyHour)
          {
            result = PublishHourType.Happy;
          }
          else
          {
            result = PublishHourType.Normal;
          }
        }
        this.calcNextSlot();
        return result;
      }
      return PublishHourType.None;
    }


  }
}
