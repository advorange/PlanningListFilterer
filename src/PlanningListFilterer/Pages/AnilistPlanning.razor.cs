﻿using MudBlazor;

using PlanningListFilterer.Models.Anilist;
using PlanningListFilterer.Models.Anilist.Json;
using PlanningListFilterer.Settings;

namespace PlanningListFilterer.Pages;

public partial class AnilistPlanning
{
	private static readonly Random _Random = new();
	private readonly HashSet<int> _RandomIds = new();

	public ColumnSettings ColumnSettings { get; set; } = new();
	public List<AnilistModel> Entries { get; set; } = new();
	public IEnumerable<AnilistModel> FilteredEntries => Grid.FilteredItems;
	public bool IsLoading { get; set; }
	public ListSettings ListSettings { get; set; } = new();
	public string? Username { get; set; } = "advorange";

	public async Task<AnilistMeta?> GetMeta(Username username)
	{
		try
		{
			return await LocalStorage.GetItemAsync<AnilistMeta>(username.Meta).ConfigureAwait(false);
		}
		catch
		{
			return null;
		}
	}

	public async Task<List<AnilistModel>> GetPlanningList(Username username, bool useCached)
	{
		var entries = default(List<AnilistModel>);
		if (useCached)
		{
			try
			{
				entries = await LocalStorage.GetItemCompressedAsync<List<AnilistModel>>(username.Name);
			}
			catch
			{
				// json error or something, nothing we can do to save this
				// retrieve new list from anilist
			}
		}
		if (entries is not null)
		{
			return entries;
		}

		var medias = new List<AnilistMedia>();
		var user = default(AnilistUser);
		await foreach (var entry in Http.GetAnilistPlanningListAsync(username.Name).ConfigureAwait(false))
		{
			user ??= entry.User;
			if (entry.Media.Status == AnilistMediaStatus.FINISHED)
			{
				medias.Add(entry.Media);
			}
		}

		if (ListSettings.EnableFriendScores)
		{
			var friends = new List<AnilistUser>();
			await foreach (var friend in Http.GetAnilistFollowingAsync(user!).ConfigureAwait(false))
			{
				friends.Add(friend);
			}
			var friendScores = new List<AnilistFriendScore>(medias.Count);
			await foreach (var friendScore in Http.GetAnilistFriendScoresAsync(medias, friends).ConfigureAwait(false))
			{
				friendScores.Add(friendScore);
			}

			entries = friendScores.ConvertAll(x =>
			{
				return AnilistModel.Create(x.Media) with
				{
					FriendScore = x.Score,
					FriendPopularityScored = x.ScoredPopularity,
					FriendPopularityTotal = x.TotalPopularity
				};
			});
		}
		else
		{
			entries = medias.ConvertAll(AnilistModel.Create);
		}

		entries.Sort((x, y) => x.Id.CompareTo(y.Id));
		await LocalStorage.SetItemCompressedAsync(username.Name, entries).ConfigureAwait(false);
		await LocalStorage.SetItemAsync(username.Meta, new AnilistMeta(
			userId: user!.Id,
			settings: ListSettings
		)).ConfigureAwait(false);

		return entries;
	}

	public async Task LoadEntries()
	{
		if (Username is null)
		{
			return;
		}

		IsLoading = true;
		// Stop showing old entries
		Entries = new List<AnilistModel>();

		var username = new Username(Username);
		var meta = await GetMeta(username).ConfigureAwait(false);
		var useCached = meta?.ShouldReacquire(ListSettings, TimeSpan.FromHours(1)) == false;

		Entries = await GetPlanningList(username, useCached).ConfigureAwait(false);
		IsLoading = false;
	}

	public async Task RandomizeTable()
	{
		var visibleEntries = Grid.FilteredItems.ToList();
		// Prevent showing duplicate random entries
		int randomId;
		do
		{
			if (_RandomIds.Count >= visibleEntries.Count)
			{
				_RandomIds.Clear();
			}

			randomId = visibleEntries[_Random.Next(0, visibleEntries.Count)].Id;
		} while (_RandomIds.Contains(randomId));

		_RandomIds.Add(randomId);
		await Grid.SetSortAsync(
			field: "Random",
			direction: SortDirection.Ascending,
			sortFunc: x => x.Id <= randomId
		).ConfigureAwait(false);
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender)
		{
			return;
		}

		ListSettings = await ListSettingsService.GetSettingsAsync().ConfigureAwait(false);
		ColumnSettings = await ColumnSettingsService.GetSettingsAsync().ConfigureAwait(false);

		var columnHidden = false;
		foreach (var column in Grid.RenderedColumns)
		{
			if (column.Hideable != false && ColumnSettings.HiddenColumns.Contains(column.PropertyName))
			{
				await column.HideAsync().ConfigureAwait(false);
				columnHidden = true;
			}
		}
		if (columnHidden)
		{
			StateHasChanged();
		}
	}
}

public sealed class Username
{
	public string Meta { get; }
	public string Name { get; }

	public Username(string username)
	{
		Name = username.ToLower();
		Meta = $"{Name}-META";
	}
}