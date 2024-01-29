using System;
using UnityEngine.Assertions;
using UnityEngine;

namespace UnityEditor.Performance.ProfileAnalyzer
{
    /// <summary>Unit type identifier</summary>
    internal enum Units
    {
        /// <summary>Time in milliseconds</summary>
        Milliseconds,
        /// <summary>Time in microseconds</summary>
        Microseconds,
        /// <summary>Count of number of instances</summary>
        Count,
    };

    internal class DisplayUnits
    {
        public static readonly string[] UnitNames =
        {
            "Milliseconds",
            "Microseconds",
            "Count",
        };

        public static readonly int[] UnitValues = (int[]) Enum.GetValues(typeof(Units));
        
        public readonly Units Units;

        public static bool kShowFullValueWhenBelowZero = true;

        public DisplayUnits(Units units)
        {
            Assert.AreEqual(UnitNames.Length, UnitValues.Length, "Number of UnitNames should match number of enum values UnitValues: You probably forgot to update one of them.");

            Units = units;
        }

        public string Postfix()
        {
            switch (Units)
            {
                default:
                case Units.Milliseconds:
                    return "ms";
                case Units.Microseconds:
                    return "us";
                case Units.Count:
                    return "";
            }
        }

        int ClampToRange(int value, int min, int max)
        {
            if (value < min)
                value = min;
            if (value > max)
                value = max;

            return value;
        }

        public string ToString(float ms, bool showUnits, int limitToNDigits, bool showFullValueWhenBelowZero = false)
        {
            float value = ms;
            int unitPower = -3;

            int maxDecimalPlaces = 0;
            float minValueShownWhenUsingLimitedDecimalPlaces = 0.5f;
            switch (Units)
            {
                default:
                case Units.Milliseconds:
                    maxDecimalPlaces = 2;
                    minValueShownWhenUsingLimitedDecimalPlaces = 0.005f;
                    break;
                case Units.Microseconds:
                    value *= 1000f;
                    unitPower -= 3;
                    maxDecimalPlaces = 0;
                    minValueShownWhenUsingLimitedDecimalPlaces = 0.5f;
                    break;
                case Units.Count:
                    showUnits = false;
                    break;
            }


            int numberOfDecimalPlaces = maxDecimalPlaces;
            int unitsTextLength = showUnits ? 2 : 0;

            if (limitToNDigits > 0)
            {
                int originalUnitPower = unitPower;

                float limitRange = (float)Math.Pow(10, limitToNDigits);

                if (limitRange > 0 && value >= limitRange)
                {
                    while (value >= 1000f && unitPower < 9)
                    {
                        value /= 1000f;
                        unitPower += 3;
                    }
                }

                if (unitPower != originalUnitPower)
                {
                    showUnits = true;
                    unitsTextLength = 2;
                }
            
                int numberOfSignificantFigures = limitToNDigits - unitsTextLength;
                int numberOfDigitsBeforeDecimalPoint = 1 + Math.Max(0, (int)Math.Log10((int)value));
                numberOfDecimalPlaces = ClampToRange(numberOfSignificantFigures - numberOfDigitsBeforeDecimalPoint, 0, maxDecimalPlaces);
            }

            string siUnitString = showUnits ? GetSIUnitString(unitPower) + "s" : "";

            bool valueWouldBeShownAsZero = value < minValueShownWhenUsingLimitedDecimalPlaces;
            string formatString = (showFullValueWhenBelowZero && valueWouldBeShownAsZero) ? string.Concat("{0}{1}") : string.Concat("{0:f", numberOfDecimalPlaces, "}{1}");
            return string.Format(formatString, value, siUnitString);
        }

        public string GetSIUnitString(int unitPower)
        {
            switch (unitPower)
            {
                case -6:
                    return "u";
                case -3:
                    return "m";
                case 0:
                    return "";
                case 3:
                    return "k";
                case 6:
                    return "m";
            }

            return "?";
        }

        public string ToString(double ms, bool showUnits, int limitToNDigits)
        {
            return ToString((float)ms, showUnits, limitToNDigits);
        }
        
        public GUIContent ToGUIContentWithTooltips(float ms, bool showUnits = false, int limitToNDigits = 5, int frameIndex = -1)
        {
            if (frameIndex>=0)
                return new GUIContent(ToString(ms, showUnits, limitToNDigits), string.Format("{0} on frame {1}", ToString(ms, true, 0, kShowFullValueWhenBelowZero), frameIndex));

            return new GUIContent(ToString(ms, showUnits, limitToNDigits), ToString(ms, true, 0, kShowFullValueWhenBelowZero));
        }
    }
}