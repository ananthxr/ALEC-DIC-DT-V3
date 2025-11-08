using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles search and relevance ranking for rooms
/// Scores rooms based on how well they match the search query
/// Higher score = more relevant
/// </summary>
public static class RoomSearchRanker
{
    /// <summary>
    /// Search rooms and return them sorted by relevance score (highest first)
    /// </summary>
    public static List<RoomData> SearchAndRank(List<RoomData> rooms, string searchQuery)
    {
        if (string.IsNullOrEmpty(searchQuery))
        {
            // No search query - return all rooms sorted alphabetically
            return rooms.OrderBy(r => r.Name).ToList();
        }

        searchQuery = searchQuery.ToLower().Trim();

        // Calculate relevance score for each room
        var rankedRooms = rooms
            .Select(room => new
            {
                Room = room,
                Score = CalculateRelevanceScore(room, searchQuery)
            })
            .Where(x => x.Score > 0) // Only include rooms with some relevance
            .OrderByDescending(x => x.Score) // Highest score first
            .ThenBy(x => x.Room.Name) // Then alphabetically for same scores
            .Select(x => x.Room)
            .ToList();

        return rankedRooms;
    }

    /// <summary>
    /// Calculate relevance score for a room based on search query
    /// Higher score = more relevant
    /// </summary>
    private static int CalculateRelevanceScore(RoomData room, string searchQuery)
    {
        int score = 0;
        string roomName = room.Name?.ToLower() ?? "";
        string floor = room.Floor?.ToLower() ?? "";

        // 1. EXACT MATCH (highest priority) +1000
        if (roomName == searchQuery)
        {
            score += 1000;
        }

        // 2. STARTS WITH (very high priority) +500
        if (roomName.StartsWith(searchQuery))
        {
            score += 500;
        }

        // 3. CONTAINS AS WHOLE WORD +300
        if (ContainsWholeWord(roomName, searchQuery))
        {
            score += 300;
        }

        // 4. CONTAINS ANYWHERE +100
        if (roomName.Contains(searchQuery))
        {
            score += 100;
        }

        // 5. FLOOR MATCHES +50
        if (floor.Contains(searchQuery))
        {
            score += 50;
        }

        // 6. WORD-BY-WORD MATCHING +10 per word
        string[] searchWords = searchQuery.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string word in searchWords)
        {
            if (roomName.Contains(word))
            {
                score += 10;
            }
        }

        // 7. INITIALS MATCH (e.g., "DIC" matches "DIC Mezzanine") +20
        if (MatchesInitials(roomName, searchQuery))
        {
            score += 20;
        }

        // 8. FUZZY MATCH (typo tolerance) +5 per character matched
        score += FuzzyMatchScore(roomName, searchQuery);

        return score;
    }

    /// <summary>
    /// Check if search query appears as a whole word
    /// </summary>
    private static bool ContainsWholeWord(string text, string word)
    {
        string[] words = text.Split(new[] { ' ', '-', '_', '/', '\\' }, System.StringSplitOptions.RemoveEmptyEntries);
        return words.Any(w => w == word);
    }

    /// <summary>
    /// Check if search query matches initials
    /// Example: "DIC" matches "DIC Mezzanine Block"
    /// </summary>
    private static bool MatchesInitials(string text, string query)
    {
        string[] words = text.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < query.Length) return false;

        string initials = string.Join("", words.Select(w => w.Length > 0 ? w[0].ToString() : "")).ToLower();
        return initials.StartsWith(query);
    }

    /// <summary>
    /// Fuzzy matching score for typo tolerance
    /// Uses Levenshtein-like approach
    /// </summary>
    private static int FuzzyMatchScore(string text, string query)
    {
        int matches = 0;
        int queryIndex = 0;

        // Count how many query characters appear in order in the text
        foreach (char c in text)
        {
            if (queryIndex < query.Length && c == query[queryIndex])
            {
                matches++;
                queryIndex++;
            }
        }

        // Only give points if a significant portion matches
        if (matches >= query.Length * 0.7) // 70% of query matched
        {
            return matches * 5;
        }

        return 0;
    }

    /// <summary>
    /// Get search suggestions based on partial query
    /// Returns top N most likely matches
    /// </summary>
    public static List<string> GetSearchSuggestions(List<RoomData> rooms, string partialQuery, int maxSuggestions = 5)
    {
        if (string.IsNullOrEmpty(partialQuery)) return new List<string>();

        partialQuery = partialQuery.ToLower().Trim();

        var suggestions = rooms
            .Where(r => r.Name != null && r.Name.ToLower().Contains(partialQuery))
            .Select(r => r.Name)
            .Distinct()
            .Take(maxSuggestions)
            .ToList();

        return suggestions;
    }
}
