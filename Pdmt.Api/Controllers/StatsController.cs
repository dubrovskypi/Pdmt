using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Pdmt.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class StatsController : ControllerBase
    {
        //GET /stats/weekly

        //Возвращает:

        //totalEvents
        //positiveEvents
        //negativeEvents
        //relationshipEvents
        //avgIntensity
        //avgTension
        //topNegativeTriggers[]
        //topPositiveEvents[]
        //timeline[] // по дням

        //GET /stats/relationships

        //Специальная вкладка:

        //totalRelationshipEvents
        //positiveInRelationships
        //negativeInRelationships
        //avgIntensityInRelationships
        //repeatPatterns[]
        //weeklyTrend[]
        //daysWithMostConflicts[]
        //daysWithPositives[]

        [HttpGet("stats/weekly")]
        public IActionResult WeeklyStats()
        {
            return NoContent();
        }
    }
}
