﻿using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Y2DL.Models;
using Y2DL.ServiceInterfaces;

namespace Y2DL.Services;

public class LoopService : BackgroundService
{
    private readonly DiscordShardedClient _client;
    private readonly Config _config;
    private readonly YoutubeService _youtubeService;
    private readonly DynamicChannelInfo _dynamicChannelInfo;
    private readonly DynamicVoiceChannelInfo _dynamicVoiceChannelInfo;
    private readonly ChannelReleases _channelReleases;

    public LoopService(DiscordShardedClient client, Config config, DynamicChannelInfo dynamicChannelInfo, ChannelReleases channelReleases, YoutubeService youtubeService, DynamicVoiceChannelInfo dynamicVoiceChannelInfo)
    {
        _client = client;
        _config = config;
        _dynamicChannelInfo = dynamicChannelInfo;
        _channelReleases = channelReleases;
        _youtubeService = youtubeService;
        _dynamicVoiceChannelInfo = dynamicVoiceChannelInfo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Listable<string> youtubeChannelIds = new Listable<string>();
                if (_config.Services.DynamicChannelInfo.Enabled)
                    youtubeChannelIds.AddRange(_config.Services.DynamicChannelInfo.Messages.Select(x => x.ChannelId));
                if (_config.Services.ChannelReleases.Enabled)
                    youtubeChannelIds.AddRange(_config.Services.ChannelReleases.Messages.Select(x => x.ChannelId));
                if (_config.Services.DynamicChannelInfoForVoiceChannels.Enabled)
                    youtubeChannelIds.AddRange(_config.Services.DynamicChannelInfoForVoiceChannels.Channels.Select(x => x.ChannelId));
                youtubeChannelIds.RemoveDuplicates();

                var channels = await _youtubeService.GetChannelsAsync(youtubeChannelIds);

                foreach (var channel in channels)
                {
                    if (_config.Services.DynamicChannelInfo.Enabled && _config.Services.DynamicChannelInfo.Messages.Exists(x => x.ChannelId == channel.Id))
                    {
                        await _dynamicChannelInfo.RunAsync(channel);
                    }
                    
                    if (_config.Services.ChannelReleases.Enabled && _config.Services.ChannelReleases.Messages.Exists(x => x.ChannelId == channel.Id))
                    {
                        await _channelReleases.RunAsync(channel);
                    }
                    
                    if (_config.Services.DynamicChannelInfoForVoiceChannels.Enabled && _config.Services.DynamicChannelInfoForVoiceChannels.Channels.Exists(x => x.ChannelId == channel.Id))
                    {
                        await _dynamicVoiceChannelInfo.RunAsync(channel);
                    }
                }
                
                await Task.Delay(_config.Main.UpdateInterval, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write(LogEventLevel.Warning, ex, "LoopService has thrown an exception");
        }
    }
}