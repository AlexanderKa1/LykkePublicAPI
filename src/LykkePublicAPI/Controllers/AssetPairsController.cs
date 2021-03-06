﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Core.Domain.Assets;
using Core.Feed;
using Core.Services;
using Lykke.Domain.Prices.Repositories;
using LykkePublicAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace LykkePublicAPI.Controllers
{
    [Route("api/[controller]")]
    public class AssetPairsController : Controller
    {
        private readonly CachedDataDictionary<string, IAssetPair> _assetPairDictionary;
        private readonly ICandleHistoryRepository _feedCandlesRepository;
        private readonly IFeedHistoryRepository _feedHistoryRepository;
        private readonly IMarketProfileService _marketProfileService;

        public AssetPairsController(
            CachedDataDictionary<string, IAssetPair> assetPairDictionary,
            ICandleHistoryRepository feedCandlesRepository, IFeedHistoryRepository feedHistoryRepository,
            IMarketProfileService marketProfileService)
        {
            _assetPairDictionary = assetPairDictionary;
            _feedCandlesRepository = feedCandlesRepository;
            _feedHistoryRepository = feedHistoryRepository;
            _marketProfileService = marketProfileService;
        }

        /// <summary>
        /// Get all asset pairs rates
        /// </summary>
        [HttpGet("rate")]
        public async Task<IEnumerable<ApiAssetPairRateModel>> GetRate()
        {
            var assetPairsIds = (await _assetPairDictionary.Values()).Where(x => !x.IsDisabled).Select(x => x.Id);

            var marketProfile = await _marketProfileService.GetMarketProfileAsync();
            marketProfile.Profile = marketProfile.Profile.Where(x => assetPairsIds.Contains(x.Asset));
            return marketProfile.ToApiModel();
        }

        /// <summary>
        /// Get rates for asset pair
        /// </summary>
        [HttpGet("rate/{assetPairId}")]
        public async Task<ApiAssetPairRateModel> GetRate(string assetPairId)
        {
            return (await _marketProfileService.GetFeedDataAsync(assetPairId))?.ToApiModel();
        }

        /// <summary>
        /// Get asset pairs dictionary
        /// </summary>
        [HttpGet("dictionary")]
        public async Task<IEnumerable<ApiAssetPair>> GetDictionary()
        {
            var pairs = (await _assetPairDictionary.Values()).Where(x => !x.IsDisabled);

            return pairs.ToApiModel();
        }

        /// <summary>
        /// Get rates for specified period
        /// </summary>
        /// <remarks>
        /// Available period values
        ///  
        ///     "Sec",
        ///     "Minute",
        ///     "Hour",
        ///     "Day",
        ///     "Month",
        /// 
        /// </remarks>
        [HttpPost("rate/history")]
        [ProducesResponseType(typeof(IEnumerable<ApiAssetPairRateModel>), 200)]
        [ProducesResponseType(typeof(ApiError), 400)]
        public async Task<IActionResult> GetHistoryRate([FromBody] AssetPairsRateHistoryRequest request)
        {
            //if (request.AssetPairIds.Length > 10)
            //    return
            //        BadRequest(new ApiError {Code = ErrorCodes.InvalidInput, Msg = "Maximum 10 asset pairs allowed" });

            if (request.Period != Period.Day)
                return
                    BadRequest(new ApiError { Code = ErrorCodes.InvalidInput, Msg = "Sorry, only day candles are available (temporary)." });

            var pairs = (await _assetPairDictionary.Values()).Where(x => !x.IsDisabled);

            if (request.AssetPairIds.Any(x => !pairs.Select(y => y.Id).Contains(x)))
                return
                    BadRequest(new ApiError {Code = ErrorCodes.InvalidInput, Msg = "Unkown asset pair id present"});

            //var candlesTasks = new List<Task<CandleWithPairId>>();

            var candles = new List<CandleWithPairId>();
            var result = new List<ApiAssetPairHistoryRateModel>();

            foreach (var pairId in request.AssetPairIds)
            {
                var askFeed = _feedHistoryRepository.GetСlosestAvailableAsync(pairId, TradePriceType.Ask, request.DateTime);
                var bidFeed = _feedHistoryRepository.GetСlosestAvailableAsync(pairId, TradePriceType.Bid, request.DateTime);

                var askCandle = (await askFeed)?.ToCandleWithPairId();
                var bidCandle = (await bidFeed)?.ToCandleWithPairId();

                if (askCandle != null && bidCandle != null)
                {
                    candles.Add(askCandle);
                    candles.Add(bidCandle);
                }
                else
                {
                    //add empty candles
                    result.Add(new ApiAssetPairHistoryRateModel {Id = pairId});
                }

                //candlesTasks.Add(_feedCandlesRepository.ReadCandleAsync(pairId, request.Period.ToDomainModel(),
                //    true, request.DateTime).ContinueWith(task => new CandleWithPairId
                //{
                //    AssetPairId = pairId,
                //    Candle = task.Result
                //}));

                //candlesTasks.Add(_feedCandlesRepository.ReadCandleAsync(pairId, request.Period.ToDomainModel(),
                //    false, request.DateTime).ContinueWith(task => new CandleWithPairId
                //{
                //    AssetPairId = pairId,
                //    Candle = task.Result
                //}));
            }

            //var candles = await Task.WhenAll(candlesTasks);

            result.AddRange(candles.ToApiModel());

            return Ok(result);
        }


        /// <summary>
        /// Get rates for specified period and asset pair
        /// </summary>
        /// <remarks>
        /// Available period values
        ///  
        ///     "Sec",
        ///     "Minute",
        ///     "Hour",
        ///     "Day",
        ///     "Month",
        /// 
        /// </remarks>
        /// <param name="assetPairId">Asset pair Id</param>
        [HttpPost("rate/history/{assetPairId}")]
        public async Task<ApiAssetPairHistoryRateModel> GetHistoryRate([FromRoute]string assetPairId,
            [FromBody] AssetPairRateHistoryRequest request)
        {
            var buyCandle = _feedCandlesRepository.GetCandleAsync(assetPairId, request.Period.ToDomainModel(),
                true, request.DateTime);

            var sellCandle = _feedCandlesRepository.GetCandleAsync(assetPairId, request.Period.ToDomainModel(),
                false, request.DateTime);

            return Convertions.ToApiModel(assetPairId, await buyCandle, await sellCandle);
        }
    }
}
