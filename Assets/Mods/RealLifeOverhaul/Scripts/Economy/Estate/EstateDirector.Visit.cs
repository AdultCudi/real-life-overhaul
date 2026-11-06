using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The one repair the player has said they will do himself (EstateVisitGame.cs): found when it is chosen, when one is
    /// answered otherwise and as a save starts, then told once a second until it is done. Nothing while none waits.
    /// </summary>
    public sealed partial class EstateDirector
    {
        private EstateVisitGame _visit;
        private string _visitPlace;   // the building of the job the player does; null while there is none
        private int _visitUnit;

        /// <summary>True while the panel at a repair's door is up, or in the frame its button ran.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._visit != null && Instance._visit.TakesKey;

        // The first call the player is to answer himself, if any: the only one the door shows.
        private void FindVisit()
        {
            string was = _visitPlace;
            _visitPlace = null;
            for (int i = 0; _state != null && Duties && i < _state.Holdings.Count && _visitPlace == null; i++)
            {
                EstateHolding h = _state.Holdings[i];
                for (int n = 0; n < h.Units.Count; n++)
                {
                    if (h.Units[n].Let && h.Units[n].Call > 0 && h.Units[n].CallAnswer == (int)CallAnswer.Yourself)
                    {
                        _visitPlace = h.Place;
                        _visitUnit = n;
                        break;
                    }
                }
            }
            if (_visitPlace == null && was != null)
            {
                CloseVisit();
            }
        }

        private void VisitSecond()
        {
            try
            {
                EstateHolding h = Duties ? _state.Holding(_visitPlace) : null;
                EstateUnit u = h != null ? h.Unit(_visitUnit) : null;
                if (u == null || u.Call <= 0 || u.CallAnswer != (int)CallAnswer.Yourself)
                {
                    FindVisit();
                    return;
                }
                if (_visit == null)
                {
                    _visit = new EstateVisitGame(this, Logger);
                }
                _visit.Second(h.Place, _visitUnit, EstateText.T("reallife_estate_visit_title", "building", EstateText.UnitName(h, _visitUnit)),
                    EstateText.T("reallife_estate_visit_what", "fault", EstateText.Fault(u.Call)), EstateCatalogue.CallMinutes[EstateCatalogue.Clamp(u.Call, 0, EstateCatalogue.CallKinds)],
                    EstateRules.DiyCost(u.CallCost));
            }
            catch (Exception e)
            {
                PartFault(EstatePart.Duties, "the repair at the door", e);
            }
        }

        private void CloseVisit()
        {
            try
            {
                _visit?.Close("no repair of the player's waits");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
