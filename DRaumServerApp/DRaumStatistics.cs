using Newtonsoft.Json;
using System;

namespace DRaumServerApp
{
  internal class DRaumStatistics
  {
    [JsonIgnore]
    private readonly object interactionMutex = new object();

    [JsonIgnore]
    private long currentIntervalInteractions;
    [JsonIgnore]
    private long lastIntervalInteractions;
    [JsonProperty]
    private long medianVotesPerPost;
    [JsonProperty]
    private volatile int medianWritersLevel;
    [JsonProperty] 
    private volatile int topWritersLevel;

    internal DRaumStatistics() 
    {
      this.medianVotesPerPost = 0;
      this.medianWritersLevel = 1;
      this.topWritersLevel = 1;
    }

    internal void updateWritersLevel(int top, int median)
    {
      this.medianWritersLevel = median;
      this.topWritersLevel = top;
    }

    internal void setVotesMedian(long median)
    {
      this.medianVotesPerPost = median;
    }

    internal long getMedianVotesPerPost()
    {
      return this.medianVotesPerPost;
    }

    internal bool isTopPost(int positiveVotesPercentage, int votes)
    {
      // Sollte schon der mittleren Aktivität entsprechen
      if(votes >= this.medianVotesPerPost)
      {
        // und eine positive Zustimmungsquote haben
        if(positiveVotesPercentage >= 50)
        {
          return true;
        }
      }
      return false;
    }

    /// TODO Statistiken über das Server-Programm (RAM-Nutzung, CPU-Last, Festplatte) ausgeben!

    internal int getPremiumLevelCap()
    {
      return this.medianWritersLevel + ((this.topWritersLevel - this.medianWritersLevel) / 2);
    }

    internal long getLastInteractionIntervalCount()
    {
      lock(this.interactionMutex)
      {
        return this.lastIntervalInteractions;
      }
    }

    internal void switchInteractionInterval()
    {
      lock(this.interactionMutex)
      {
        this.lastIntervalInteractions = this.currentIntervalInteractions;
        this.currentIntervalInteractions = 0;
      }
    }

    internal void increaseInteraction()
    {
      lock(this.interactionMutex)
      {
        this.currentIntervalInteractions++;
      }
    }
  }
}