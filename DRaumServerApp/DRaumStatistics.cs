using Hardware.Info;
using McpNetwork.SystemMetrics;
using McpNetwork.SystemMetrics.Models;
using Newtonsoft.Json;
using System;
using System.Text;


namespace DRaumServerApp
{
  internal class DRaumStatistics
  {
    [JsonIgnore]
    private readonly object interactionMutex = new object();

    [JsonIgnore]
    private static readonly HardwareInfo hardwareInfo = new HardwareInfo();

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
      this.medianVotesPerPost = 1;
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
      if (median > 1)
      {
        this.medianVotesPerPost = median;
      }
      else
      {
        this.medianVotesPerPost = 1;
      }
    }

    internal long getMedianVotesPerPost()
    {
      return this.medianVotesPerPost;
    }

    internal bool isTopPost(long upVotes, long votes)
    {
      // Sollte schon der mittleren Aktivität entsprechen
      if (votes >= this.medianVotesPerPost)
      {
        // und eine positive Zustimmungsquote haben
        if (upVotes >= votes / 2)
        {
          return true;
        }
      }
      return false;
    }

    internal String getHardwareInfo()
    {
      hardwareInfo.RefreshMemoryStatus();
      hardwareInfo.RefreshDriveList();
      StringBuilder sb = new StringBuilder();
      sb.Append((((double)hardwareInfo.MemoryStatus.AvailablePhysical / 1024.0) / 1024.0).ToString("0.00"));
      sb.Append(" MB free physical mem\r\n");
      foreach (var drive in hardwareInfo.DriveList)
      {
        foreach (var partition in drive.PartitionList)
        {
          foreach (var volume in partition.VolumeList)
          {
            sb.Append(volume.Name);
            sb.Append(" has ");
            sb.Append((((double)volume.FreeSpace / 1024.0) / 1024.0).ToString("0.0"));
            sb.Append(" MB free space");
            sb.Append("\r\n");
          }
        }
      }
      SystemMetrics systemMetrics = new SystemMetrics();
      Metrics result = systemMetrics.GetMetrics();
      sb.Append(result.TotalCpuUsage.ToString("0.0"));
      sb.Append(" % CPU Usage");
      return sb.ToString();
    }

    internal int getPremiumLevelCap()
    {
      return this.medianWritersLevel + ((this.topWritersLevel - this.medianWritersLevel) / 2);
    }

    internal long getLastInteractionIntervalCount()
    {
      lock (this.interactionMutex)
      {
        return this.lastIntervalInteractions;
      }
    }

    internal void switchInteractionInterval()
    {
      lock (this.interactionMutex)
      {
        this.lastIntervalInteractions = this.currentIntervalInteractions;
        this.currentIntervalInteractions = 0;
      }
    }

    internal void increaseInteraction()
    {
      lock (this.interactionMutex)
      {
        this.currentIntervalInteractions++;
      }
    }
  }
}