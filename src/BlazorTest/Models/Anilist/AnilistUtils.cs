﻿using System.Net.Mime;
using System.Text.Json;
using System.Text;
using System.Net.Http.Json;
using BlazorTest.Models.Anilist.Json;

namespace BlazorTest.Models.Anilist;

public static class AnilistUtils
{
	public const string GRAPHQL_QUERY = @"
	query ($username: String) {
		MediaListCollection(userName: $username, type: ANIME, status: PLANNING) {
			lists {
				name
				isCustomList
				isCompletedList: isSplitCompletedList
				entries {
					media {
						id
						title {
							userPreferred
						}
						status
						format
						episodes
						duration
						averageScore
						popularity
						startDate {
							year,
							month
						}
						coverImage {
							medium
						}
						genres
						tags {
							name
							rank
						}
						relations {
							edges {
								node {
									id
									type
									startDate {
										year
										month
									}
								}
								relationType
							}
						}
					}
				}
			}
		}
	}
	";
	public const string GRAPHQL_URL = "https://graphql.anilist.co";
	public const string NO_VALUE = "N/A";

	public static AnilistStartModel CreateStartModel(this AnilistMedia media)
	{
		return new(
			Year: media.StartDate?.Year,
			Month: media.StartDate?.Month
		);
	}

	public static string DisplayDuration(this AnilistModel model)
	{
		var duration = model.GetTotalDuration();
		if (!duration.HasValue)
		{
			return NO_VALUE;
		}
		return $"{duration} minute{(duration == 1 ? "" : "s")}";
	}

	public static string DisplayEpisodeCount(this AnilistModel model)
	{
		var count = model.GetHighestEpisode();
		if (!count.HasValue)
		{
			return NO_VALUE;
		}
		return count.Value.ToString();
	}

	public static string DisplayFormat(this AnilistModel model)
		=> model.Format?.ToString() ?? NO_VALUE;

	public static string DisplayGenres(this AnilistModel model)
		=> model.Genres.DisplayStrings();

	public static string DisplayScore(this AnilistModel model)
	{
		var score = model.AverageScore;
		return score.HasValue ? $"{score}%" : NO_VALUE;
	}

	public static string DisplayStart(this AnilistModel model)
	{
		var year = model.Start.Year;
		if (!year.HasValue)
		{
			return NO_VALUE;
		}

		var month = model.Start.Month;
		var format = month.HasValue ? "yyyy MMM" : "yyyy";
		return model.Start.Time!.Value.ToString(format);
	}

	public static string DisplayTag(this KeyValuePair<string, int> tag)
	{
		var name = tag.Key;
		if (name is "Cute Girls Doing Cute Things")
		{
			name = "CGDCT";
		}
		return $"{name} ({tag.Value}%)";
	}

	public static string DisplayTags(this IEnumerable<KeyValuePair<string, int>> tags)
		=> tags.Select(x => x.DisplayTag()).DisplayStrings();

	public static string DisplayTags(this AnilistModel model, int skip, int count)
	{
		return model.Tags
			.OrderByDescending(x => x.Value)
			.Skip(skip)
			.Take(count)
			.DisplayTags();
	}

	public static async Task<AnilistResponse> GetAnilistAsync(
		this HttpClient http,
		string username)
	{
		var body = JsonSerializer.Serialize(new
		{
			query = GRAPHQL_QUERY,
			variables = new
			{
				username,
			}
		});
		var content = new StringContent(
			content: body,
			encoding: Encoding.UTF8,
			mediaType: MediaTypeNames.Application.Json
		);

		var request = new HttpRequestMessage(
			method: HttpMethod.Post,
			requestUri: GRAPHQL_URL
		)
		{
			Content = content
		};
		request.Headers.Add("Accept", MediaTypeNames.Application.Json);

		using var response = await http.SendAsync(request).ConfigureAwait(false);
		using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

		return (await JsonSerializer.DeserializeAsync<AnilistResponse>(
			utf8Json: stream
		).ConfigureAwait(false))!;
	}

	public static async Task<AnilistResponse> GetAnilistSampleAsync(
		this HttpClient http)
	{
		return (await http.GetFromJsonAsync<AnilistResponse>(
			requestUri: $"sample-data/anilistresponse.json?a={Guid.NewGuid()}"
		).ConfigureAwait(false))!;
	}

	public static int? GetHighestEpisode(this AnilistModel model)
		=> model.Episodes ?? model.NextAiringEpisode;

	public static int? GetTotalDuration(this AnilistModel model)
		=> model.GetHighestEpisode() * model.Duration;

	public static string GetUrl(this AnilistModel model)
		=> $"https://anilist.co/anime/{model.Id}/";

	private static string DisplayStrings(this IEnumerable<string> items)
	{
		if (!items.Any())
		{
			return NO_VALUE;
		}
		return string.Join(Environment.NewLine, items);
	}
}