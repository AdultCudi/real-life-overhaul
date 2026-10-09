using BigAmbitions.DayNightCycle;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The chess player on the pavement: the mod's chess table with one stool taken by somebody
    /// from the living city's pool and the other left free for the player. Put out of the
    /// player's view on a wide piece of pavement by day in the dry, at the places the living
    /// city's own chess scene tests (both stools on the pavement, room either side), and taken
    /// in again out of view when the player has gone, the light fails or it rains. One person,
    /// one prop, asked once a second by the director. Never while the living city has its own
    /// chess table out: one table at a time.
    /// </summary>
    public sealed class ChessCorner
    {
        private const float StoolOut = 0.62f;   // a stool's centre from the table's (chess_sit)
        private const int FromHour = 9, ToHour = 20;

        private CityPerson _opponent;
        private Transform _table;
        private float _nextTry;

        public bool Out => _table != null;
        public Vector3 Table { get; private set; }
        public Vector3 Stool { get; private set; }      // the free one
        public CityPerson Opponent => _opponent;
        public bool OpponentThere => _opponent != null && !_opponent.Lost;

        /// <summary>Once a second while the player is out of doors. <paramref name="busy"/>: a game is on at the table.</summary>
        public void Tick(Vector3 player, float now, bool busy)
        {
            CityLifeDirector city = CityLifeDirector.Instance;
            if (city == null)
            {
                TakeIn();
                return;
            }
            bool wanted = Settings.Leisure && Settings.LeisureChess && Settings.CityLife && !RainHelper.isRaining
                          && TimeHelper.CurrentHour >= FromHour && TimeHelper.CurrentHour < ToHour;
            if (Out)
            {
                if (busy)
                {
                    return;
                }
                bool gone = CitySpots.Flat(Table - player) > 140f;
                if (!OpponentThere || gone || (!wanted && !CitySpots.InView(Table)))
                {
                    TakeIn();
                    _nextTry = now + 45f;
                }
                return;
            }
            if (!wanted || now < _nextTry)
            {
                return;
            }
            _nextTry = now + 25f;
            if (city.IsOut("chess") || city.People.Free < 3 || !ClipLibrary.Has("chess_sit"))
            {
                return;   // the living city keeps the larger share of its own pool
            }
            if (CitySpots.PavementRing(player, 30f, 70f, true, out Vector3 anchor))
            {
                PutOut(anchor, player, out _);
            }
        }

        /// <summary>Stands the table and seats the opponent. False with the reason; nothing is left out then.</summary>
        public bool PutOut(Vector3 anchor, Vector3 player, out TestRefusal why)
        {
            why = TestRefusal.NothingFits;
            CityLifeDirector city = CityLifeDirector.Instance;
            if (city == null || Out)
            {
                why = city == null ? TestRefusal.FeatureOff : TestRefusal.SceneActive;
                return false;
            }
            if (!CitySpots.Stand(anchor, 1.25f, out Vector3 table))
            {
                return false;
            }
            if (!CitySpots.Along(table, out Vector3 along))
            {
                along = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
            }
            // The stools are the table's own +Z and -Z. The player's is the one nearer to them, and must be walkable to.
            if (Vector3.Dot(player - table, along) < 0f)
            {
                along = -along;
            }
            Vector3 mine = table + along * StoolOut;
            Vector3 theirs = table - along * StoolOut;
            if (!CitySpots.OnPavement(mine, out _) || !CitySpots.OnPavement(theirs, out _)
                || !CitySpots.OnPavement(table + along * 1.2f, out Vector3 approach) || !CitySpots.OnPavement(table - along * 1.2f, out _)
                || !CitySpots.ClearQuietly(approach, 0.3f))
            {
                return false;
            }
            Transform prop = city.Props.Stand("chess_table", table, Quaternion.LookRotation(along).eulerAngles.y);
            if (prop == null)
            {
                why = TestRefusal.NotLoaded;   // the prop bundle is not in yet
                return false;
            }
            if (!prop.TryGetComponent(out NavMeshObstacle solid))
            {
                // The same obstacle the living city's chess scene gives the table: people walk round it.
                solid = prop.gameObject.AddComponent<NavMeshObstacle>();
                solid.shape = NavMeshObstacleShape.Box;
                solid.center = new Vector3(0f, 0.5f, 0f);
                solid.size = new Vector3(0.8f, 1f, 0.8f);
                solid.carving = false;
            }
            CityPerson person = city.People.Take(CityLook.Civilian, theirs, table);
            if (person == null || !person.Work("chess_sit", city.Props, 0f))
            {
                if (person != null)
                {
                    city.People.Release(person);
                }
                city.Props.Return(prop);
                why = TestRefusal.PoolBusy;
                return false;
            }
            _table = prop;
            _opponent = person;
            Table = table;
            Stool = mine;
            LeisureDirector.Logger?.Info("Leisure: a chess player set up at " + table.ToString("F0") + ", " + Mathf.RoundToInt(CitySpots.Flat(table - player)) + " m away.");
            return true;
        }

        public void TakeIn()
        {
            CityLifeDirector city = CityLifeDirector.Instance;
            try
            {
                if (city != null)
                {
                    if (_opponent != null)
                    {
                        city.People.Release(_opponent);
                    }
                    if (_table != null)
                    {
                        city.Props.Return(_table);
                    }
                }
            }
            catch (System.Exception e)
            {
                LeisureDirector.Logger?.Error(e);
            }
            _opponent = null;
            _table = null;
        }
    }

    /// <summary>
    /// Street chess against whoever sits at the table. A whole game of chess is not what a
    /// player stops on the pavement for (and a real engine and board interface would be a mod of
    /// their own), so this is five positions from a game, each with three moves to choose from
    /// on a draining clock: one is the move, one is sound, one loses material. The opponent
    /// answers each in a line. Seven points of ten win the stake, five draw it. It is a reading
    /// game rather than a timing one because chess is: the clock only stops it being a quiz.
    /// </summary>
    public sealed class ChessGame : LeisureActivity
    {
        public const int Stake = 10;
        private const int Moves = 5;
        private const float ThinkSeconds = 22f;
        private const float ReplySeconds = 3.6f;

        private sealed class Position
        {
            public readonly string Situation;
            public readonly string[] MoveKeys;
            public readonly string[] ReplyKeys;
            public readonly int[] Worth;   // 2 the move, 1 sound, 0 a blunder

            public Position(string situation, string m1, string m2, string m3, string r1, string r2, string r3, int w1, int w2, int w3)
            {
                Situation = situation;
                MoveKeys = new[] { m1, m2, m3 };
                ReplyKeys = new[] { r1, r2, r3 };
                Worth = new[] { w1, w2, w3 };
            }
        }

        private static readonly Position[] Positions =
        {
            new Position("reallife_leisure_chess_p1",
                "reallife_leisure_chess_p1_m1", "reallife_leisure_chess_p1_m2", "reallife_leisure_chess_p1_m3",
                "reallife_leisure_chess_p1_r1", "reallife_leisure_chess_p1_r2", "reallife_leisure_chess_p1_r3", 2, 1, 0),
            new Position("reallife_leisure_chess_p2",
                "reallife_leisure_chess_p2_m1", "reallife_leisure_chess_p2_m2", "reallife_leisure_chess_p2_m3",
                "reallife_leisure_chess_p2_r1", "reallife_leisure_chess_p2_r2", "reallife_leisure_chess_p2_r3", 0, 2, 1),
            new Position("reallife_leisure_chess_p3",
                "reallife_leisure_chess_p3_m1", "reallife_leisure_chess_p3_m2", "reallife_leisure_chess_p3_m3",
                "reallife_leisure_chess_p3_r1", "reallife_leisure_chess_p3_r2", "reallife_leisure_chess_p3_r3", 1, 0, 2),
            new Position("reallife_leisure_chess_p4",
                "reallife_leisure_chess_p4_m1", "reallife_leisure_chess_p4_m2", "reallife_leisure_chess_p4_m3",
                "reallife_leisure_chess_p4_r1", "reallife_leisure_chess_p4_r2", "reallife_leisure_chess_p4_r3", 2, 0, 1),
            new Position("reallife_leisure_chess_p5",
                "reallife_leisure_chess_p5_m1", "reallife_leisure_chess_p5_m2", "reallife_leisure_chess_p5_m3",
                "reallife_leisure_chess_p5_r1", "reallife_leisure_chess_p5_r2", "reallife_leisure_chess_p5_r3", 1, 2, 0),
            new Position("reallife_leisure_chess_p6",
                "reallife_leisure_chess_p6_m1", "reallife_leisure_chess_p6_m2", "reallife_leisure_chess_p6_m3",
                "reallife_leisure_chess_p6_r1", "reallife_leisure_chess_p6_r2", "reallife_leisure_chess_p6_r3", 0, 1, 2),
            new Position("reallife_leisure_chess_p7",
                "reallife_leisure_chess_p7_m1", "reallife_leisure_chess_p7_m2", "reallife_leisure_chess_p7_m3",
                "reallife_leisure_chess_p7_r1", "reallife_leisure_chess_p7_r2", "reallife_leisure_chess_p7_r3", 2, 1, 0),
            new Position("reallife_leisure_chess_p8",
                "reallife_leisure_chess_p8_m1", "reallife_leisure_chess_p8_m2", "reallife_leisure_chess_p8_m3",
                "reallife_leisure_chess_p8_r1", "reallife_leisure_chess_p8_r2", "reallife_leisure_chess_p8_r3", 1, 2, 0)
        };

        private enum Step { Walk, Sit, Think, Reply, Over }

        private readonly ChessCorner _corner;
        private readonly int[] _order = new int[Positions.Length];

        private Step _step;
        private float _stepEnds;
        private bool _moved;            // chess_move_player is on, held at its end
        private float _thinkFrom;
        private int _move;
        private int _points;
        private bool _paid;

        public ChessGame(ChessCorner corner)
        {
            _corner = corner;
        }

        public override LeisureKind Kind => LeisureKind.Chess;
        public override string TitleKey => "reallife_leisure_chess_title";

        public override bool Begin(float now, out string refusalKey)
        {
            refusalKey = null;
            if (!_corner.Out || !_corner.OpponentThere)
            {
                refusalKey = "reallife_leisure_chess_no_table";
                return false;
            }
            if (CitySpots.Flat(_corner.Stool - Ctx.Position) > 4f)
            {
                refusalKey = "reallife_leisure_chess_too_far";
                return false;
            }
            if (!ClipLibrary.Has("chess_sit") || !Ctx.Body.Usable)
            {
                refusalKey = "reallife_leisure_no_clips";
                return false;
            }
            if (!LeisureMoney.Charge(Stake, "reallife_transaction_chess_stake"))
            {
                refusalKey = "reallife_leisure_no_money";
                return false;
            }
            _paid = true;
            Anchor = _corner.Table;
            Leash = 7f;
            for (int i = 0; i < _order.Length; i++)
            {
                _order[i] = i;
            }
            for (int i = _order.Length - 1; i > 0; i--)
            {
                int k = Random.Range(0, i + 1);
                int swap = _order[i];
                _order[i] = _order[k];
                _order[k] = swap;
            }
            _step = Step.Walk;
            _stepEnds = now + 20f;
            Ctx.Player.SetGoal(_corner.Stool, null);   // walked there by the game itself, unless they are steering: then they walk it
            Marked = true;
            MarkAt = _corner.Stool;
            MarkLabel = T("reallife_leisure_chess_stool");
            Show(T("reallife_leisure_chess_sit_down"));
            return true;
        }

        public override void Tick(float now)
        {
            if (!_corner.OpponentThere)
            {
                Refund();
                Finish(false);   // knocked off the stool, or taken for something else
                return;
            }
            switch (_step)
            {
                case Step.Walk:
                    if (CitySpots.Flat(_corner.Stool - Ctx.Position) < 0.5f)
                    {
                        Ctx.Body.Hold();
                        if (!Ctx.Body.Settle(_corner.Stool))
                        {
                            Refund();
                            Finish(false);
                            return;
                        }
                        Ctx.Body.Face(_corner.Table);
                        Marked = false;
                        _step = Step.Sit;
                        _stepEnds = now + 0.6f;
                    }
                    else if (now >= _stepEnds)
                    {
                        Refund();
                        Finish(false);   // something stood in the way of the stool
                    }
                    break;

                case Step.Sit:
                    if (now >= _stepEnds)
                    {
                        if (!Ctx.Body.Play("chess_sit", null, 0.35f))
                        {
                            Refund();
                            Finish(false);
                            return;
                        }
                        _move = 0;
                        _points = 0;
                        Deal(now + 1.2f);
                    }
                    break;

                case Step.Think:
                    if (now - _thinkFrom >= ThinkSeconds)
                    {
                        Answer(-1, now);
                    }
                    break;

                case Step.Reply:
                    if (now >= _stepEnds)
                    {
                        _move++;
                        if (_moved)
                        {
                            _moved = false;
                            Ctx.Body.Play("chess_sit", null, 0.25f);   // the move ends in the sitting loop's first pose
                        }
                        if (_move >= Moves)
                        {
                            Conclude(now);
                        }
                        else
                        {
                            Deal(now);
                        }
                    }
                    break;

                case Step.Over:
                    if (now >= _stepEnds)
                    {
                        Finish(true);   // on their feet again
                    }
                    break;
            }
        }

        public override void Frame(float now, float delta)
        {
            if (_step == Step.Think)
            {
                MeterCursor = 1f - Mathf.Clamp01((now - _thinkFrom) / ThinkSeconds);   // the clock, draining
            }
        }

        public override void Choose(int choice, float now)
        {
            if (_step == Step.Think && now >= _thinkFrom)
            {
                Answer(choice, now);
            }
        }

        private void Deal(float from)
        {
            Position position = Positions[_order[_move]];
            var labels = new string[3];   // a new row each move: the HUD redraws a row only when it is a different one
            for (int i = 0; i < 3; i++)
            {
                labels[i] = T(position.MoveKeys[i]);
            }
            Choices = labels;
            Show(T(position.Situation), T("reallife_leisure_chess_status", "move", (_move + 1).ToString(), "points", _points.ToString()));
            Meter = true;
            ZoneCentre = 0f;
            ZoneWidth = 0f;
            SweetWidth = 0f;
            MeterLabel = T("reallife_leisure_chess_clock");
            MeterCursor = 1f;
            _thinkFrom = from;
            _step = Step.Think;
        }

        private void Answer(int choice, float now)
        {
            Position position = Positions[_order[_move]];
            Choices = null;
            Meter = false;
            if (choice < 0 || choice > 2)
            {
                Show(T("reallife_leisure_chess_flag"), T("reallife_leisure_chess_status", "move", (_move + 1).ToString(), "points", _points.ToString()));
                LeisureSound.Cue(LeisureCue.Bad);
            }
            else
            {
                _points += position.Worth[choice];
                // The hand makes the move (seated as chess_sit, from and back to its first pose); held at its end until
                // the answer has been read, then the sitting loop again. Without the clip they just go on sitting.
                _moved = ClipLibrary.Has("chess_move_player") && Ctx.Body.Play("chess_move_player", null, 0.2f, ClipEnd.Hold);
                Show(T(position.ReplyKeys[choice]), T("reallife_leisure_chess_status", "move", (_move + 1).ToString(), "points", _points.ToString()));
                LeisureSound.Cue(position.Worth[choice] == 2 ? LeisureCue.Good : position.Worth[choice] == 1 ? LeisureCue.Tick : LeisureCue.Bad);
            }
            _step = Step.Reply;
            _stepEnds = now + ReplySeconds;
        }

        private void Conclude(float now)
        {
            _step = Step.Over;
            _stepEnds = now + 0.7f;
            Ctx.Body.Rest(0.5f);   // up off the stool before the hold is let go
            Score = _points;
            string outcome;
            if (_points >= 7)
            {
                LeisureMoney.Give(Stake * 2, "reallife_transaction_chess_win");
                outcome = T("reallife_leisure_chess_won", "amount", JobText.Money(Stake));
                Mood = ModState.MoodUp;
            }
            else if (_points >= 5)
            {
                LeisureMoney.Give(Stake, "reallife_transaction_chess_win");
                outcome = T("reallife_leisure_chess_drawn");
                Mood = ModState.Social;
            }
            else
            {
                outcome = T("reallife_leisure_chess_lost", "amount", JobText.Money(Stake));
            }
            _paid = false;
            CardTitle = T("reallife_leisure_chess_title");
            CardBody = T("reallife_leisure_chess_result", "points", _points.ToString()) + "\n" + outcome;
            LeisureMoney.Tire(1.5f);
        }

        // A game that never got going costs nothing.
        private void Refund()
        {
            if (_paid)
            {
                _paid = false;
                LeisureMoney.Give(Stake, "reallife_transaction_chess_win");
            }
        }

        public override void End()
        {
            if (!Played && _paid && _step <= Step.Sit)
            {
                Refund();   // walked off before the first move
            }
            // Walking off in the middle of a game forfeits the stake: nothing more to do. The opponent stays at the table.
        }
    }
}
