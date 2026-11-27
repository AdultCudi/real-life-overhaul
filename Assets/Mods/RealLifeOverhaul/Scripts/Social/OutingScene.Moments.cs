using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The outing's part for a moment (SocialMomentRules): where the outing ends, now and then something happens and the game's own
    /// dialogue panel asks how the player reacts. One small hook in <see cref="Wrap"/> starts it, and it hands the outing back to
    /// <see cref="Wrap"/> when the panel closes. COST: nothing while no moment is on; while one is, the talk's own tick.
    /// </summary>
    public sealed partial class OutingScene
    {
        private const float MomentMost = 240f;       // seconds a moment's panel is waited for before the outing goes on without it

        private bool _momentShown;                   // this outing had a moment
        private bool _momentDone;                    // ...and it is over
        private bool _momentWell;                    // how the outing went, handed back to Wrap
        private MomentDef _moment;
        private MomentPlay _play;
        private bool _momentClosed;
        private float _momentFrom;

        // Called first thing in Wrap. True when a moment was begun: Wrap is called again when it is over.
        private bool StartMoment(float now, bool wentWell)
        {
            if (_momentDone || _momentShown || !wentWell || _friend == null || _friend.Lost || !_greeted || !SocialMomentRules.HasMoments(_kind))
            {
                return false;
            }
            if (_test ? !SocialMomentRules.On : !SocialMomentRules.Occurs(_who, _kind))
            {
                return false;   // (a test's outing always has one, so the owner can see it)
            }
            MomentDef def = SocialMomentRules.Pick(_who, _kind);
            if (def == null)
            {
                return false;
            }
            _moment = def;
            _momentShown = true;
            _momentWell = wentWell;
            _momentClosed = false;
            _momentFrom = Time.time;
            Prompt = null;
            Line = null;
            Unmark();
            _play = new MomentPlay { Def = def, Place = _venue.Valid ? _venue.Name : string.Empty, Choose = ApplyMoment };
            var talk = new SceneTalk { Person = _who, Mode = SceneTalk.Moment, Play = _play };
            if (Rides)
            {
                Talk = talk;   // nobody is stood up or turned in a car: the panel alone
                SocialDirector.Talk = talk;
                SocialPhone.OpenInPerson(_friend.Character, () => _momentClosed = true);
            }
            else
            {
                Converse(talk, _friend, now);
            }
            Go(Step.Moment, now, 0f);
            if (Meta.Dev.Verbose)
            {
                SocialDirector.Logger?.Info("Social check (moment): " + _who.Name + "'s " + _kind + " has the moment " + def.Id + " (" + def.Tone + "), shown in the panel: PASS.");
            }
            return true;
        }

        private void TickMoment(float now)
        {
            bool ended = Rides ? _momentClosed || !SocialPhone.DialogOpen : TickTalk(now, _friend);
            if (!ended && Time.time - _momentFrom < MomentMost)
            {
                return;
            }
            SocialDirector.Talk = null;
            _momentDone = true;
            Wrap(now, _momentWell);
        }

        // The reaction chosen: weighed against who they are, the figures moved, the memory kept, and the words that say so.
        private string ApplyMoment(int i)
        {
            MomentDef d = _moment;
            MomentReaction r = d.Reactions[i];
            bool paid = true;
            if (r.Cost > 0)
            {
                paid = SocialLife.Pay(Prices.At(r.Cost), _def.Transaction ?? "reallife_transaction_social_coffee");
            }
            MomentResult res = SocialMomentRules.Weigh(_who, d, i, _inUniform);
            if (!paid)
            {
                res = new MomentResult { Tier = MomentTier.Flat, Familiarity = 0.5f, Affection = 0.5f };
            }
            if (!_test)
            {
                SocialLife.Change(_who, res.Familiarity, res.Affection, res.Trust);
                SocialLife.Remember(_who, SocialMomentRules.MemoryOf(d.Tone));
                _who.MomentLast = d.Id;
                _who.MomentDay = SocialLife.Day;
                _who.MomentEcho = true;
                _who.MomentRow = true;
                SocialPassionGame.Clue(_who, res.Clue, "the moment " + d.Id);
            }
            Dictionary<string, string> data = SocialLife.Data(_who, "place", _venue.Valid ? _venue.Name : string.Empty);
            _text.Length = 0;
            _text.Append(SocialLife.T(paid ? (res.Pleased ? r.Good : r.Flat) : SocialMomentCatalogue.CannotPay, data)).Append("\n\n");
            string[] closers = SocialMomentRules.Closers(res.Tier);
            _text.Append(SocialLife.T(closers[SocialPassionRules.Variant(_who, SocialLife.Day, closers.Length, 83)], data)).Append("\n");
            int at = SocialPassionRules.IndexOf(res.Led);
            if (res.Led != SocialTrait.None && at >= 0)
            {
                string line = SocialPassionGame.CardLine(_who, at, res.Passion);
                if (line != null)
                {
                    _text.Append(line).Append("\n");
                }
            }
            _text.Append("\n");
            CardLine("reallife_social_card_familiarity", Signed(res.Familiarity) + NextOf(SocialMeter.Familiarity));
            CardLine("reallife_social_card_affection", Signed(res.Affection) + NextOf(SocialMeter.Affection));
            CardLine("reallife_social_card_trust", Signed(res.Trust) + NextOf(SocialMeter.Trust));
            if (_test)
            {
                _text.Append(SocialLife.T("reallife_social_card_test"));
            }
            if (Meta.Dev.Verbose)
            {
                SocialDirector.Logger?.Info("Social check (moment): " + _who.Name + " weighed reaction " + i + " (" + r.Style + ") of moment " + d.Id + " at score "
                                            + res.Score.ToString("F2", CultureInfo.InvariantCulture) + " (passion " + res.Passion.ToString("F2", CultureInfo.InvariantCulture) + "): "
                                            + res.Tier + ", familiarity " + res.Familiarity.ToString("F1", CultureInfo.InvariantCulture) + " affection "
                                            + res.Affection.ToString("F1", CultureInfo.InvariantCulture) + " trust " + res.Trust.ToString("F1", CultureInfo.InvariantCulture)
                                            + (_test ? " (a test: nothing recorded)" : string.Empty) + ": PASS.");
            }
            return _text.ToString();
        }
    }
}
