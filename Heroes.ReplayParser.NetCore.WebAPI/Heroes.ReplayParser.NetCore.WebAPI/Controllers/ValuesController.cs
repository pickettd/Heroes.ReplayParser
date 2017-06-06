using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Heroes.ReplayParser;
using Foole.Mpq;
using Newtonsoft.Json;
using System.Reflection;

namespace Heroes.ReplayParser.NetCore.WebAPI.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        public Replay replay;
        public List<string> replayPropNames;
        public ValuesController() {

            replayPropNames = new List<string>();
            foreach (var replayProp in typeof(Replay).GetProperties()) {
                replayPropNames.Add(replayProp.Name);
            }

            // NOTE: this code assumes that your replays are in the following directories of the project. It just chooses one.
            replay = new Replay();
            var heroesAccountsFolder = "./__replays";
            var randomReplayFileName = Directory.GetFiles(heroesAccountsFolder, "*.StormReplay", SearchOption.AllDirectories).OrderBy(i => Guid.NewGuid()).First();
            Console.WriteLine("File name is " + randomReplayFileName);

            // Use temp directory for MpqLib directory permissions requirements
            //var tmpPath = Path.GetTempFileName();
            //File.Copy(randomReplayFileName, tmpPath, overwrite: true);
            try
            {
                // Attempt to parse the replay
                // Ignore errors can be set to true if you want to attempt to parse currently unsupported replays, such as 'VS AI' or 'PTR Region' replays
                var replayParseResult = DataParser.ParseReplay(randomReplayFileName, ignoreErrors: false, deleteFile: false);

                // If successful, the Replay object now has all currently available information
                if (replayParseResult.Item1 == DataParser.ReplayParseResult.Success)
                {
                    replay = replayParseResult.Item2;
                    Console.WriteLine(typeof(Replay).GetProperties(BindingFlags.Instance));

                    Console.WriteLine("Replay Build: " + replay.ReplayBuild);
                    Console.WriteLine("Map: " + replay.Map);
                    foreach (var player in replay.Players.OrderByDescending(i => i.IsWinner))
                        Console.WriteLine("Player: " + player.Name + ", Win: " + player.IsWinner + ", Hero: " + player.Character + ", Lvl: " + player.CharacterLevel + ", Talents: " + string.Join(",", player.Talents.Select(i => i.TalentID + ":" + i.TalentName)));
                }
                else
                    Console.WriteLine("Failed to Parse Replay: " + replayParseResult.Item1);

            }
            finally
            {
                //if (File.Exists(tmpPath))
                //    File.Delete(tmpPath);
            }
        }
        private static byte[] GetMpqArchiveFileBytes(MpqArchive archive, string fileName)
        {
            using (var mpqStream = archive.OpenFile(archive.Single(i => i.Filename == fileName)))
            {
                var buffer = new byte[mpqStream.Length];
                mpqStream.Read(buffer, 0, buffer.Length);
                return buffer;
            }
        }
        // GET api/values
        [HttpGet]
        public JsonResult Get()
        {
            return Json(replayPropNames);
        }
        
        // GET api/values/Map etc
        [HttpGet("{inputProp}")]
        public string Get(string inputProp)
        {
            if ((inputProp == "GameEvents")||(inputProp == "TrackerEvents")) {
                return "This will take too long to serialize";
            }
            var obj = typeof(Replay).GetProperty(inputProp).GetValue(replay, null);
            //return Json(obj);
            return JsonConvert.SerializeObject(obj, Formatting.Indented,
                new JsonSerializerSettings() {
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects
                }
            );
        }

        // GET api/values/5
        /*[HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public JsonResult Post([FromBody]string value)
        {
            return Json(value);
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }*/
    }
}
