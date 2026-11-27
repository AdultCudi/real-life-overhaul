using System;
using System.Collections;
using Helpers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The social director's part for the love life: an evening offered at a party's end, a night under a black screen with the game's
    /// time machine, and the morning after in the panel. Nothing per frame: a party's offer waits for the player to be free, checked on the
    /// director's one-second round.
    /// </summary>
    public sealed partial class SocialDirector
    {
        private const float PartyOfferSeconds = 600f;   // real seconds a party's offer waits for the player to be free

        private bool _night;                            // a night is passing: nothing else of the social life starts
        private LoveOffer _partyOffer;
        private float _partyOfferUntil;

        internal bool NightOn => _night;

        // A guest who came to a party the player was at: the evening may go on, once, for the first who offers.
        private void PartyGuest(SocialPerson guest, int quality)
        {
            try
            {
                if (_partyOffer != null || _night || guest == null || quality < 55)
                {
                    return;
                }
                LoveOffer offer = LoveNight.Offer(guest, NightKind.Party, true, 2f);
                if (offer != null)
                {
                    _partyOffer = offer;
                    _partyOfferUntil = Time.unscaledTime + PartyOfferSeconds;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The party's offer, put to the player when no scene, conversation or question has them.
        private void LoveSlow(float real)
        {
            if (_partyOffer == null)
            {
                return;
            }
            if (real > _partyOfferUntil || !LoveHooks.HookupsOn || _night)
            {
                _partyOffer = null;
                return;
            }
            if (_scene != null || DialogController.current != null || SocialPhone.DialogOpen || HudConfirm.isOpen || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            LoveOffer offer = _partyOffer;
            _partyOffer = null;
            Talk = new SceneTalk { Person = offer.Person, Mode = SceneTalk.Love, Offer = offer };
            SocialPhone.OpenInPerson(null, () =>
            {
                Talk = null;
                if (offer.Night)
                {
                    BeginNight(offer);
                }
            });
        }

        /// <summary>The offer was taken: the screen goes dark, the night is settled and the clock moves to the morning, then the morning is told.</summary>
        internal void BeginNight(LoveOffer offer)
        {
            if (_night || offer == null)
            {
                return;
            }
            StartCoroutine(Guard.Routine(Night(offer)));
        }

        private IEnumerator Night(LoveOffer offer)
        {
            _night = true;
            UIs uis = InstanceBehavior<UIs>.Instance;
            try
            {
                yield return UiFader.Fade(0.6f, "reallife_love_night_caption");
                try
                {
                    LoveNight.Settle(offer);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                if (uis != null && uis.timeMachine != null && !uis.timeMachine.isRunning && !GoingPublic.ClockShared)
                {
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Bench);
                    uis.timeMachine.StartTimeMachine(TimeHelper.GetTimeInHours(LoveRules.NightHours(offer.Facts.Hour)), true, "reallife_love_night_caption");
                    while (uis.timeMachine.isRunning)
                    {
                        yield return null;
                    }
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                    yield return null;
                }
                yield return UiFader.UnFade();
                if (offer.Hook != null)
                {
                    bool closed = false;
                    float waited = 0f;
                    while ((DialogController.current != null || HudConfirm.isOpen) && waited < 20f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Talk = new SceneTalk { Person = offer.Person, Mode = SceneTalk.Love, Offer = offer };
                    SocialPhone.OpenInPerson(null, () => closed = true);
                    waited = 0f;
                    while (!closed && waited < 300f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Talk = null;
                    ShowCard(SocialLife.T("reallife_love_card_night", SocialLife.Data(offer.Person)), LoveTalk.CardOf(offer));
                }
            }
            finally
            {
                _night = false;
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                if (UiFader.isFading)
                {
                    StartCoroutine(UiFader.UnFade());
                }
            }
        }
    }
}
