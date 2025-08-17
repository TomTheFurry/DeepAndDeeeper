using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Shashlichnik
{
    public static class JobGiver_Patch
    {
        public static string[] overridingJobDefNames = new string[] { "LayDown", "Ingest" };

        private static void TryOverrideJob(Pawn pawn, ref Job job)
        {
            Log.Message($"[JobGiver_Patch] Checking job for pawn {pawn.Name}: [{job?.def?.defName}]");
            
            if (job?.def?.defName is { } val
                && overridingJobDefNames.Contains(val))
            {
                Log.Message($"[JobGiver_Patch] Found overridable job: {val}");
                
                var caveComp = pawn.Map.GetComponent<CaveMapComponent>();
                Log.Message($"[JobGiver_Patch] Cave component: {caveComp != null}, exit: {caveComp?.caveExit != null}, spawned: {caveComp?.caveExit?.Spawned}, exitIfNoJob: {caveComp?.caveExit?.exitIfNoJob}");
                
                if (caveComp != null && caveComp.caveExit != null && caveComp.caveExit.Spawned &&
                    caveComp.caveExit.exitIfNoJob)
                {
                    // Check if this is a hunger-related job and pawn can't find food in the cave
                    if (val == "Ingest" && !CanFindFoodInCave(pawn, caveComp))
                    {
                        Log.Message($"[JobGiver_Patch] Detected hunger but no food available in cave. Redirecting pawn to exit cave.");
                        var oldjob = job;
                        JobMaker.ReturnToPool(oldjob); // Return the old job to the pool
                        // make a new one
                        job = JobMaker.MakeJob(JobDefOf.EnterPortal, caveComp.caveExit);
                    }
                    // Check if this is sleeping on floor (original functionality)
                    else if (val == "LayDown")
                    {
                        Log.Message($"[JobGiver_Patch] Detected overridable job [{val}]. Redirecting the pawn to exit cave instead.");
                        var oldjob = job;
                        JobMaker.ReturnToPool(oldjob); // Return the old job to the pool
                        // make a new one
                        job = JobMaker.MakeJob(JobDefOf.EnterPortal, caveComp.caveExit);
                    }
                    else
                    {
                        Log.Message($"[JobGiver_Patch] Job {val} not handled or conditions not met");
                    }
                }
                else
                {
                    Log.Message($"[JobGiver_Patch] Cave exit conditions not met for override");
                }
            }
        }

        private static bool CanFindFoodInCave(Pawn pawn, CaveMapComponent caveComp)
        {
            Map caveMap = caveComp.caveExit.Map;
            
            // Look for any food sources in the cave that the pawn can access
            var foodSources = caveMap.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(food => food.def.IsNutritionGivingIngestible && 
                              !food.IsForbidden(pawn) && 
                              pawn.CanReach(food, PathEndMode.OnCell, Danger.Deadly));
            
            return foodSources.Any();
        }

        // patch return result
        [HarmonyLib.HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
        internal static class GetRest_TryGiveJob_Patch
        {
            public static void Postfix(Pawn pawn, ref Job __result)
            {
                Log.Message($"[JobGiver_Patch] GetRest patch called for {pawn.Name}, result: {__result?.def?.defName}");
                TryOverrideJob(pawn, ref __result);
            }
        }

        // patch return result for food (hunger)
        [HarmonyLib.HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
        internal static class GetFood_TryGiveJob_Patch
        {
            public static void Postfix(Pawn pawn, ref Job __result)
            {
                Log.Message($"[JobGiver_Patch] GetFood patch called for {pawn.Name}, result: {__result?.def?.defName}");
                TryOverrideJob(pawn, ref __result);
            }
        }
    }
}