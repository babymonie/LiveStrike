// Example configuration for different sites
module.exports = {
    // HLTV.org live match page
    hltv: {
        url: 'https://www.hltv.org/matches/2367309/example-match',
        selectors: {
            team1Name: '.team1-side .teamName, .team .team-name:first-of-type',
            team2Name: '.team2-side .teamName, .team .team-name:last-of-type',
            score1: '.team1-side .won, .team1 .score',
            score2: '.team2-side .won, .team2 .score',
            killFeed: '.round-history .kill, .killfeed .kill-entry',
            liveIndicator: '.match-status.live, .live-indicator',
            mapName: '.map-name, .current-map',
            roundNumber: '.round-number, .current-round'
        }
    },

    // ESL Play
    esl: {
        url: 'https://play.eslgaming.com/match/12345678',
        selectors: {
            team1Name: '.match-team:first-child .team-name',
            team2Name: '.match-team:last-child .team-name',
            score1: '.match-team:first-child .score',
            score2: '.match-team:last-child .score',
            killFeed: '.killfeed .kill-entry',
            liveIndicator: '.match-status.live'
        }
    },

    // Twitch (for streamers with overlay data)
    twitch: {
        url: 'https://www.twitch.tv/example-stream',
        selectors: {
            team1Name: '[data-team="1"] .team-name',
            team2Name: '[data-team="2"] .team-name',
            score1: '[data-team="1"] .score',
            score2: '[data-team="2"] .score',
            killFeed: '.kill-feed .kill',
            liveIndicator: '.live-indicator'
        }
    },

    // FACEIT
    faceit: {
        url: 'https://www.faceit.com/en/csgo/room/1-example-match',
        selectors: {
            team1Name: '.team-left .team-name',
            team2Name: '.team-right .team-name',
            score1: '.team-left .team-score',
            score2: '.team-right .team-score',
            killFeed: '.match-log .log-entry',
            liveIndicator: '.match-status.live'
        }
    },

    // Custom example (adjust for your specific site)
    custom: {
        url: 'https://example.com/match/12345',
        selectors: {
            team1Name: '.team-1 .name',
            team2Name: '.team-2 .name',
            score1: '.team-1 .score',
            score2: '.team-2 .score',
            killFeed: '.killfeed .entry',
            liveIndicator: '.status.live'
        }
    }
};