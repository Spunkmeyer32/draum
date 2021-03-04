using System;
using System.Collections;
using System.Collections.Concurrent;
using DRaumServerApp.Postings;
using JetBrains.Annotations;
using Newtonsoft.Json;


namespace DRaumServerApp.Authors
{
  internal class AuthorManager
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    public static int Maxmanagedusers = 100; // int.max

    [JsonProperty]
    private readonly ConcurrentDictionary<long, Author> authors;    

    internal AuthorManager()
    {
      if (Utilities.Runningintestmode)
      {
        Maxmanagedusers = int.MaxValue;
      }
      this.authors = new ConcurrentDictionary<long, Author>();
    }

    [CanBeNull]
    private Author getAuthor(long authorId)
    {
      return this.authors.ContainsKey(authorId) ? this.authors[authorId] : null;
    }

    
    private Author getAuthor(long authorId, string externalName)
    {
      if (this.authors.ContainsKey(authorId))
      {
        if (externalName != null && !this.authors[authorId].getAuthorName().Equals(externalName))
        {
          // Usernamen aktualisieren
          this.authors[authorId].setAuthorName(externalName);
        }
        return this.authors[authorId];
      }
      else
      {
        if (this.authors.Count < Maxmanagedusers)
        {
          externalName ??= "";
          Author newAuthor = new Author(authorId, externalName);
          if (this.authors.TryAdd(authorId, newAuthor))
          {
            logger.Info("Neuer Autor: " + externalName + " (" + authorId + ")");
            return newAuthor;
          }
          else
          {
            throw new DRaumException("Aufgrund eines Fehlers konnte der Nutzer nicht hinzugefügt werden. Bitter später erneut probieren oder einen Administrator kontaktieren.");
          }
        }
        else
        {
          logger.Warn("Ein Nutzer wurde abgewisen, da die Maximale Nutzerzahl erreicht ist");
          throw new DRaumException("Maximale Nutzeranzahl erreicht");
        }
      }
    }

    internal bool canUserVote(long postingid, long authorId, string authorName)
    {
      Author author = this.getAuthor(authorId, authorName);
      if (author != null)
      {
        try
        {
          return author.canVote(postingid);
        }
        catch (Exception e)
        {
          logger.Error(e,"Konnte den Autor " + authorId + " nicht abfragen, ob er eine Stimme abgeben darf");
        }
      }
      return false;
    }

    internal bool canUserFlag(long postingid, long authorId, string authorName)
    {
      Author author = this.getAuthor(authorId, authorName);
      if (author != null)
      {
        try
        {
          return author.canFlag(postingid);
        }
        catch (Exception e)
        {
          logger.Error(e,"Konnte den Autor " + authorId + " nicht abfragen, ob er einen Beitrag markieren darf");
        }
      }
      return false;
    }

    

    internal void getMedianAndTopLevel(out int medianOut, out int topOut)
    {
      // alle Autoren Prüfen und Median und Top ermitteln
      try
      {
        int toplevel = 0;
        ArrayList levellist = new ArrayList();
        foreach (Author author in this.authors.Values)
        {
          int temp = author.getLevel();
          levellist.Add(temp);
          if (temp > toplevel)
          {
            toplevel = temp;
          }
        }
        Array target = levellist.ToArray();
        Array.Sort(target);
        if (target.Length == 0)
        {
          medianOut = 0;
          topOut = 0;
          return;
        }
        // ReSharper disable once PossibleNullReferenceException
        int median = (int) target.GetValue(target.Length / 2);
        medianOut = median;
        topOut = toplevel;
      }
      catch (Exception e)
      {
        logger.Error(e,"Konnte Median- und Top-Level nicht ermitteln");
        medianOut = 0;
        topOut = 0;
      }
    }

    internal int getAuthorCount()
    {
      return this.authors.Count;
    }

    internal void setPostMode(long id, string externalName)
    {
      this.getAuthor(id, externalName)?.setPostMode();
    }

    internal PostingPublishManager.PublishHourType getPublishType(long authorId, int premiumLevelCap)
    {
      Author author = this.getAuthor(authorId);
      return author?.getPublishType(premiumLevelCap) ?? PostingPublishManager.PublishHourType.None;
    }

    internal bool isPostMode(long id, string externalName)
    {
      Author author = this.getAuthor(id, externalName);
      return author?.isInPostMode() ?? false;
    }

    internal void unsetModes(long id, string externalName)
    {
      this.getAuthor(id, externalName)?.unsetModes();
    }

    internal void setFeedbackMode(long id, string externalName)
    {
      this.getAuthor(id, externalName)?.setFeedbackMode();
    }

    internal bool isFeedbackMode(long id, string externalName)
    {
      Author author = this.getAuthor(id, externalName);
      return author?.isInFeedbackMode() ?? false;
    }

    internal bool isCoolDownOver(long id, string externalName, Author.InteractionCooldownTimer timerType)
    {
      Author author = this.getAuthor(id, externalName);
      return author?.coolDownOver(timerType) ?? false;
    }

    internal void resetCoolDown(long id, string externalName, Author.InteractionCooldownTimer timerType)
    {
      this.getAuthor(id, externalName)?.resetCoolDown(timerType);
    }

    internal TimeSpan getCoolDownTimer(long id, string externalName, Author.InteractionCooldownTimer timerType)
    {
      Author author = this.getAuthor(id, externalName);
      return author?.getCoolDownTimer(timerType) ?? TimeSpan.MaxValue;
    }

    internal string getAuthorPostText(long authorId)
    {
      Author author = this.getAuthor(authorId);
      if(author == null)
      {
        return "<i>Schreiber/in nicht gefunden!</i>\r\n<i>Verfasst im D-Raum https://t.me/d_raum </i>";
      }
      return "<i>" + author.getShortAuthorInfo() + "</i>\r\n<i>Verfasst im D-Raum https://t.me/d_raum </i>";
    }

    internal void vote(long postingId, long authorId)
    {
      this.getAuthor(authorId)?.vote(postingId);
    }

    internal void flag(long postingId, long authorId)
    {
      this.getAuthor(authorId)?.flag(postingId);
    }


    internal void updateCredibility(long authorId, long receivedUpVotes, long receivedDownVotes)
    {
      Author author = this.getAuthor(authorId);
      if (author != null)
      {
        author.updateCredibility(receivedUpVotes, receivedDownVotes);
      }
      else
      {
        logger.Warn("Konnte votes dem Autor mit der ID " + authorId + " nicht zuordnen");
      }
    }

    internal void publishedSuccessfully(long authorId)
    {
      Author author = this.getAuthor(authorId);
      if (author != null)
      {
        author.publishedSuccessfully();
      }
      else
      {
        logger.Warn("Konnte die Veröffentlichung dem Autor mit der ID " + authorId + " nicht gutschreiben");
      }
    }

    internal int voteUpAndGetCount(long authorId, string username)
    {
      Author author = this.getAuthor(authorId, username);
      return author?.voteUpAndGetCount() ?? 0;
    }

    internal int voteDownAndGetCount(long authorId, string username)
    {
      Author author = this.getAuthor(authorId,username);
      return author?.voteDownAndGetCount() ?? 0;
    }


    public void blockForDays(long authorId, int days)
    {
      this.getAuthor(authorId)?.blockForDays(days);
    }
  }
    
}
