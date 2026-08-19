VITEC Scoreboard v0.8.3 Game Weather Fix

Fixes the v0.8.2 compile error:
- Removed IMemoryCache from VS.MLB.
- Removed dependency on Microsoft.Extensions.Caching.Memory.
- Replaced it with a built-in ConcurrentDictionary schedule-context cache.
- Keeps 6-hour schedule caching behavior.
- Keeps MLB game weather support.
- Keeps player metadata, structured events, Statcast fields, series context, and all v0.8.x live features.

Update:
  installer\Update-Game-Weather-Fix.cmd
