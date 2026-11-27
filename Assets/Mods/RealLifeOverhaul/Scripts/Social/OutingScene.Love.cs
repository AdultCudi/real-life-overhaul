using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The outing's part for an evening that may go on after drinks: the offer, in the panel, and what follows (Social/Love).</summary>
    public sealed partial class OutingScene
    {
        private bool _loveAsked;
        private LoveOffer _loveOffer;

        // After drinks that went well, at the end, where the goodbye would be: they may offer to go on. True while the offer is on the panel.
        private bool LoveAfterwards(float now)
        {
            if (_loveAsked)
            {
                return false;
            }
            _loveAsked = true;
            if (_test || _kind != PlanKind.Drinks || _closed || _friend == null || _friend.Lost)
            {
                return false;
            }
            LoveOffer offer = LoveNight.Offer(_who, NightKind.Drinks, _wentWell, 0f);
            if (offer == null)
            {
                return false;
            }
            _loveOffer = offer;
            Converse(new SceneTalk { Person = _who, Mode = SceneTalk.Love, Offer = offer }, _friend, now);
            Go(Step.Late, now, 0f);
            return true;
        }

        // The panel closed: a night begins under a black screen, or the goodbye goes on as ever.
        private void LoveAnswered(float now)
        {
            LoveOffer o = _loveOffer;
            if (o != null && o.Night && D != null)
            {
                D.Cast.Release(_friend);
                _friend = null;
                D.BeginNight(o);
                Over = true;
                return;
            }
            Go(Step.Wrap, now, 0f);
        }

        // What an outing with one of two people comes to for the other, and a drinks outing counts as drinks.
        private void LoveSettled()
        {
            if (_test)
            {
                return;
            }
            if (_kind == PlanKind.Drinks && LoveHooks.HookupsOn)
            {
                LoveNight.Drink(LoveNight.OutingDrinks);
            }
            LoveLife.OutingDone(_who, _kind, _venue);
        }
    }
}
