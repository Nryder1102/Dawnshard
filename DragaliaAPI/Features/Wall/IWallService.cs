﻿using DragaliaAPI.Models.Generated;
using DragaliaAPI.Shared.Definitions.Enums;

namespace DragaliaAPI.Features.Wall;

public interface IWallService
{
    Task LevelupQuestWall(int wallId);

    Task SetQuestWallIsStartNextLevel(int wallId, bool value);

    Task<IEnumerable<QuestWallList>> GetQuestWallList();

    Task<int> GetTotalWallLevel();

    Task GrantMonthlyRewardEntityList(IEnumerable<AtgenBuildEventRewardEntityList> rewards);

    IEnumerable<AtgenBuildEventRewardEntityList> GetMonthlyRewardEntityList(int levelTotal);

    IEnumerable<AtgenUserWallRewardList> GetUserWallRewardList(
        int levelTotal,
        RewardStatus rewardStatus
    );
}
