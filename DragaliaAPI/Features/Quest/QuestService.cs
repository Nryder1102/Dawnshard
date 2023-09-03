using System.Diagnostics;
using DragaliaAPI.Database.Entities;
using DragaliaAPI.Database.Repositories;
using DragaliaAPI.Features.Missions;
using DragaliaAPI.Features.Reward;
using DragaliaAPI.Helpers;
using DragaliaAPI.Models;
using DragaliaAPI.Models.Generated;
using DragaliaAPI.Services.Exceptions;
using DragaliaAPI.Shared.Definitions.Enums;
using DragaliaAPI.Shared.MasterAsset;
using DragaliaAPI.Shared.MasterAsset.Models;
using DragaliaAPI.Shared.MasterAsset.Models.QuestDrops;

namespace DragaliaAPI.Features.Quest;

public class QuestService(
    ILogger<QuestService> logger,
    IQuestRepository questRepository,
    IDateTimeProvider dateTimeProvider,
    IResetHelper resetHelper,
    IQuestCacheService questCacheService,
    IRewardService rewardService,
    IMissionProgressionService missionProgressionService
) : IQuestService
{
    public async Task<(
        DbQuest Quest,
        bool BestClearTime,
        IEnumerable<AtgenFirstClearSet> Bonus
    )> ProcessQuestCompletion(int questId, float clearTime, int playCount)
    {
        DbQuest quest = await questRepository.GetQuestDataAsync(questId);
        quest.State = 3;

        bool isBestClearTime = false;

        if (0 > quest.BestClearTime || quest.BestClearTime > clearTime)
        {
            quest.BestClearTime = clearTime;
            isBestClearTime = true;
        }

        quest.PlayCount += playCount;

        if (resetHelper.LastDailyReset > quest.LastDailyResetTime)
        {
            logger.LogTrace("Resetting daily play count for quest {questId}", questId);

            quest.DailyPlayCount = 0;
            quest.LastDailyResetTime = dateTimeProvider.UtcNow;
        }

        quest.DailyPlayCount += playCount;

        if (resetHelper.LastWeeklyReset > quest.LastWeeklyResetTime)
        {
            logger.LogTrace("Resetting weekly play count for quest {questId}", questId);

            quest.WeeklyPlayCount = 0;
            quest.LastWeeklyResetTime = dateTimeProvider.UtcNow;
        }

        quest.WeeklyPlayCount += playCount;

        IEnumerable<AtgenFirstClearSet> questEventRewards = Enumerable.Empty<AtgenFirstClearSet>();

        QuestData questData = MasterAsset.QuestData[questId];

        missionProgressionService.OnQuestCleared(
            questId,
            questData.Gid,
            questData.QuestPlayModeType,
            1,
            quest.PlayCount
        );

        if (questData.IsEventQuest)
        {
            int baseGroupId = MasterAsset.QuestEventGroup[questData.Gid].BaseQuestGroupId;

            await questCacheService.SetQuestGroupQuestIdAsync(baseGroupId, questId);

            questEventRewards = await ProcessQuestEventCompletion(
                baseGroupId,
                questData,
                playCount
            );
        }

        return (quest, isBestClearTime, questEventRewards);
    }

    private async Task<IEnumerable<AtgenFirstClearSet>> ProcessQuestEventCompletion(
        int eventGroupId,
        QuestData questData,
        int playCount
    )
    {
        logger.LogTrace("Completing quest for quest group {eventGroupId}", eventGroupId);

        DbQuestEvent questEvent = await questRepository.GetQuestEventAsync(eventGroupId);
        QuestEvent questEventData = MasterAsset.QuestEvent[eventGroupId];

        if (resetHelper.LastDailyReset > questEvent.LastDailyResetTime)
        {
            if (questEventData.QuestBonusType == QuestResetIntervalType.Daily)
            {
                ResetQuestEventBonus(questEvent, questEventData);
            }

            questEvent.DailyPlayCount = 0;
            questEvent.LastDailyResetTime = dateTimeProvider.UtcNow;
        }

        questEvent.DailyPlayCount += playCount;

        if (resetHelper.LastWeeklyReset > questEvent.LastWeeklyResetTime)
        {
            if (questEventData.QuestBonusType == QuestResetIntervalType.Weekly)
            {
                ResetQuestEventBonus(questEvent, questEventData);
            }

            questEvent.WeeklyPlayCount = 0;
            questEvent.LastWeeklyResetTime = dateTimeProvider.UtcNow;
        }

        questEvent.WeeklyPlayCount += playCount;

        // NOTE: Do we ever need total here?
        missionProgressionService.OnEventGroupCleared(
            eventGroupId,
            questData.VariationType,
            questData.QuestPlayModeType,
            playCount,
            1
        );

        if (questEventData.QuestBonusCount == 0)
        {
            return Enumerable.Empty<AtgenFirstClearSet>();
        }

        int totalBonusCount = questEvent.QuestBonusReserveCount + questEvent.QuestBonusReceiveCount;
        int remainingBonusCount = questEventData.QuestBonusCount - totalBonusCount;

        if (remainingBonusCount <= 0)
        {
            if (questEvent.QuestBonusStackCount > 0)
            {
                questEvent.QuestBonusStackCount--;
                questEvent.QuestBonusStackTime = dateTimeProvider.UtcNow;
                remainingBonusCount++;
            }
            else
            {
                return Enumerable.Empty<AtgenFirstClearSet>();
            }
        }

        int bonusesToReceive = Math.Min(playCount, remainingBonusCount);

        if (questEventData.QuestBonusReceiveType != QuestBonusReceiveType.AutoReceive)
        {
            questEvent.QuestBonusReserveCount += bonusesToReceive;
            questEvent.QuestBonusReserveTime = dateTimeProvider.UtcNow;

            return Enumerable.Empty<AtgenFirstClearSet>();
        }

        questEvent.QuestBonusReceiveCount += bonusesToReceive;

        return (await GenerateBonusDrops(questData.Id, bonusesToReceive)).Select(
            x => x.ToFirstClearSet()
        );
    }

    public async Task<IEnumerable<Entity>> GenerateBonusDrops(int questId, int count)
    {
        List<Entity> drops = new();

        if (
            !MasterAsset.QuestBonusRewards.TryGetValue(
                questId,
                out QuestBonusReward? questBonusReward
            )
        )
        {
            logger.LogWarning("Failed to retrieve bonus rewards for quest {questId}", questId);
            return drops;
        }

        for (int i = 0; i < count; i++)
        {
            foreach (QuestBonusDrop drop in questBonusReward.Bonuses)
            {
                Entity entity = Entity.FromQuestBonusDrop(drop);
                await rewardService.GrantReward(entity);
                drops.Add(entity);
            }
        }

        return drops;
    }

    public async Task<AtgenReceiveQuestBonus> ReceiveQuestBonus(
        int eventGroupId,
        bool isReceive,
        int count
    )
    {
        DbQuestEvent questEvent = await questRepository.GetQuestEventAsync(eventGroupId);

        if (!isReceive)
        {
            questEvent.QuestBonusReserveCount = 0;
            questEvent.QuestBonusReserveTime = DateTimeOffset.UnixEpoch;

            await questCacheService.RemoveQuestGroupQuestIdAsync(eventGroupId);

            return new AtgenReceiveQuestBonus();
        }

        int questId =
            await questCacheService.GetQuestGroupQuestIdAsync(eventGroupId)
            ?? throw new DragaliaException(
                ResultCode.CommonDbError,
                $"Could not find latest quest clear id for group {eventGroupId} in cache."
            );

        if (questEvent.QuestBonusReserveCount > 0)
        {
            questEvent.QuestBonusReserveCount--;
            questEvent.QuestBonusReserveTime = DateTimeOffset.UnixEpoch;
        }
        else
        {
            throw new DragaliaException(
                ResultCode.QuestSelectBonusReceivableLimit,
                "Tried to receive too many drops"
            );
        }

        questEvent.QuestBonusReceiveCount++;

        // TODO: bonus factor?
        IEnumerable<AtgenBuildEventRewardEntityList> bonusRewards = (
            await GenerateBonusDrops(questId, count)
        ).Select(x => x.ToBuildEventRewardEntityList());

        // Remove at the end so it doesn't get messed up when erroring
        await questCacheService.RemoveQuestGroupQuestIdAsync(eventGroupId);

        return new AtgenReceiveQuestBonus(questId, count, 1, bonusRewards);
    }

    private void ResetQuestEventBonus(DbQuestEvent questEvent, QuestEvent questEventData)
    {
        if (questEventData.QuestBonusReceiveType != QuestBonusReceiveType.AutoReceive)
        {
            if (questEventData.QuestBonusStackCountMax > 0)
            {
                (DateTimeOffset lastReset, TimeSpan resetInterval) =
                    questEventData.QuestBonusType switch
                    {
                        QuestResetIntervalType.Daily
                            => (questEvent.LastDailyResetTime, TimeSpan.FromDays(1)),
                        QuestResetIntervalType.Weekly
                            => (questEvent.LastWeeklyResetTime, TimeSpan.FromDays(7)),
                        _ => throw new UnreachableException()
                    };

                while (
                    dateTimeProvider.UtcNow >= lastReset + resetInterval
                    && questEventData.QuestBonusStackCountMax - 1 > questEvent.QuestBonusStackCount
                )
                {
                    questEvent.QuestBonusStackCount++;
                }

                questEvent.QuestBonusStackTime = dateTimeProvider.UtcNow;
            }

            questEvent.QuestBonusReserveCount = 0;
            questEvent.QuestBonusReserveTime = dateTimeProvider.UtcNow;
        }

        questEvent.QuestBonusReceiveCount = 0;

        logger.LogDebug("Reset quest event counts to {@questEvent}", questEvent);
    }
}
