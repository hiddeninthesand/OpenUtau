using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenUtau.Api;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Plugin.Builtin
{
	[Phonemizer("Korean VCV Phonemizer", "KO VCV", "ldc")]

	public class KoreanVCVPhonemizer : Phonemizer
	{
		/// <summary>
		/// Initial jamo as ordered in Unicode
		/// </summary>
		static readonly string[] initials = { "g", "gg", "n", "d", "dd", "r", "m", "b", "bb", "s", "ss", string.Empty, "j", "jj", "ch", "k", "t", "p", "h" };
		// ㄱ ㄲ ㄴ ㄷ ㄸ ㄹ ㅁ ㅂ ㅃ ㅅ ㅆ ㅇ ㅈ ㅉ ㅊ ㅋ ㅌ ㅍ ㅎ

		/// <summary>
		/// Medial jamo as ordered in Unicode
		/// </summary>
		static readonly string[] medials = { "a", "e", "ya", "ye", "eo", "e", "yeo", "ye", "o", "wa", "we", "we", "yo", "u", "weo", "we", "wi", "yu", "eu", "eui", "i" };
		// ㅏ ㅐ ㅑ ㅒ ㅓ ㅔ ㅕ ㅖ ㅗ ㅘ ㅙ ㅚ ㅛ ㅜ ㅝ ㅞ ㅟ ㅠ ㅡ ㅢ ㅣ

		/// <summary>
		/// Final jamo as ordered in Unicode
		/// </summary>
		static readonly string[] finals = { string.Empty, "k", "k", "k", "n", "n", "n", "t", "l", "l", "l", "l", "l", "l", "l", "l", "m", "p", "p", "t", "t", "ng", "t", "t", "k", "t", "p", "t" };
		// - ㄱ ㄲ ㄳ ㄴ ㄵ ㄶ ㄷ ㄹ ㄺ ㄻ ㄼ ㄽ ㄾ ㄿ ㅀ ㅁ ㅂ ㅄ ㅅ ㅆ ㅇ ㅈ ㅊ ㅋ ㅌ ㅍ ㅎ

		/// <summary>
		/// Sonorant batchim (i.e., extendable batchim sounds)
		/// </summary>
		static readonly string[] sonorants = { "n", "m", "ng", "l" };

		/// <summary>
		/// Extra English-based sounds for phonetic hint input + alternate romanizations for tense plosives (ㄲ, ㄸ, ㅃ)
		/// </summary>
		static readonly string[] extras = { "f", "v", "th", "dh", "z", "kk", "pp", "tt" };

		/// <summary>
		/// Gets the romanized initial, medial, and final components of the passed Hangul syllable.
		/// </summary>
		/// <param name="syllable">A Hangul syllable.</param>
		/// <returns>An array containing the initial, medial, and final sounds of the syllable.</returns>
		public string[] GetIMF(string syllable)
		{
			byte[] bytes = Encoding.Unicode.GetBytes(syllable);
			int numval = Convert.ToInt32(bytes[0]) + Convert.ToInt32(bytes[1]) * (16 * 16);
			numval -= 44032;
			int i = numval / 588;
			numval -= i * 588;
			int m = numval / 28;
			numval -= m * 28;
			int f = numval;

			string[] ret = { initials[i], medials[m], finals[f] };

			return ret;
		}

		/// <summary>
		/// Separates the initial, medial, and final components of the passed phonetic hint.
		/// </summary>
		/// <param name="hint">A phonetic hint.</param>
		/// <returns>An array containing the initial, medial, and final sounds of the phonetic hint.</returns>
		public string[] GetIMFFromHint(string hint)
		{
			string[] hintSplit = hint.Split(' ');

			string i = Array.IndexOf(initials.Concat(extras).ToArray(), hintSplit[0]) > -1 ? hintSplit[0] : string.Empty;
			string m = string.IsNullOrEmpty(i) ? hintSplit[0] : hintSplit[1];
			string f = (hintSplit.Length > 2 || (hintSplit.Length == 2 && string.IsNullOrEmpty(i))) ? hintSplit[^1] : string.Empty;

			if (i == "kk" || i == "pp" || i == "tt") i = i.Replace('k', 'g').Replace('p', 'b').Replace('t', 'd');

			if (m == "oe" || m == "oi") m = "we";
			if (m == "ui") m = "eui";
			if (m == "ae" || m == "wae" || m == "yae") m = m.Replace("ae", "e");

			string[] ret = { i, m, f };

			return ret;
		}

		/// <summary>
		/// Gets the last sound of an alias.
		/// </summary>
		/// <param name="lyric">The alias to get the last sound of.</param>
		/// <returns>The last sound of the alias</returns>
		public string GetLastSoundOfAlias(string lyric)
		{
			string lastSound = lyric.Split(' ')[^1];
			Regex symbolRemove = new Regex(@"\W");
			MatchCollection symbolMatches = symbolRemove.Matches(lastSound);

			foreach (Match symbolMatch in symbolMatches)
			{
				lastSound = lastSound.Replace(symbolMatch.Value, string.Empty);
			}

			if (Array.IndexOf(finals, lastSound) == -1)
			{
				foreach (string i in initials)
				{
					if (!string.IsNullOrEmpty(i)) lastSound = lastSound.Replace(i, string.Empty);
				}
			}

			return lastSound;
		}

		private USinger singer;

		// Store singer
		public override void SetSinger(USinger singer) => this.singer = singer;

		public override Result Process(Note[] notes, Note? prev, Note? next, Note? prevNeighbour, Note? nextNeighbour, Note[] prevNeighbours)
		{
			Note note = notes[0];
			string color = string.Empty;
			int shift = 0;

			PhonemeAttributes attr = note.phonemeAttributes.FirstOrDefault(a => a.index == 0);
			color = attr.voiceColor;
			shift = attr.toneShift;

			string[] currIMF;
			string currPhoneme;
			string[] prevIMF;

			// Check if lyric is R or - and return appropriate Result; otherwise, move to next steps
			if (note.lyric == "R" || note.lyric == "-")
			{
				currPhoneme = note.lyric;

				if (prevNeighbour == null)
				{
					return new Result
					{
						phonemes = new Phoneme[] {
							new Phoneme { phoneme = currPhoneme }
						}
					};
				}
				else
				{
					if (singer.TryGetMappedOto(prevNeighbour?.lyric, note.tone, color, out _)) {
						string lastSound = GetLastSoundOfAlias(prevNeighbour?.lyric);
						currPhoneme = $"{(!singer.TryGetMappedOto($"{lastSound} {currPhoneme}", note.tone, color, out _) ? lastSound.ToUpper() : lastSound)} {currPhoneme}";
					}

					else {
						if (string.IsNullOrEmpty(prevNeighbour?.phoneticHint)) {
							byte[] bytes = Encoding.Unicode.GetBytes($"{prevNeighbour?.lyric[0]}");
							int numval = Convert.ToInt32(bytes[0]) + Convert.ToInt32(bytes[1]) * (16 * 16);
							if (prevNeighbour?.lyric.Length == 1 && numval >= 44032 && numval <= 55215) prevIMF = GetIMF(prevNeighbour?.lyric);
							else return new Result {
								phonemes = new Phoneme[] {
									new Phoneme { phoneme = currPhoneme }
								}
							};
						} else prevIMF = GetIMFFromHint(prevNeighbour?.phoneticHint);

						if (string.IsNullOrEmpty(prevIMF[2])) currPhoneme = $"{((prevIMF[1][0] == 'w' || prevIMF[1][0] == 'y' || prevIMF[1] == "oi") ? prevIMF[1].Remove(0, 1) : ((prevIMF[1] == "eui" || prevIMF[1] == "ui") ? "i" : prevIMF[1]))} {currPhoneme}";
						else currPhoneme = $"{(!singer.TryGetMappedOto($"{prevIMF[2]} {currPhoneme}", note.tone, color, out _) ? prevIMF[2].ToUpper() : prevIMF[2])} {currPhoneme}";
					}
					
					return new Result {
						phonemes = new Phoneme[] {
							new Phoneme { phoneme = currPhoneme }
						}
					};
				}
			}

			// Get IMF of current note if valid, otherwise return the lyric as is
			if (string.IsNullOrEmpty(note.phoneticHint))
			{
				byte[] bytes = Encoding.Unicode.GetBytes($"{note.lyric[0]}");
				int numval = Convert.ToInt32(bytes[0]) + Convert.ToInt32(bytes[1]) * (16 * 16);
				if (note.lyric.Length == 1 && numval >= 44032 && numval <= 55215) currIMF = GetIMF(note.lyric);
				else return new Result
				{
					phonemes = new Phoneme[] {
						new Phoneme { phoneme = note.lyric }
					}
				};
			}
			else currIMF = GetIMFFromHint(note.phoneticHint);

			// Convert current note to phoneme
			currPhoneme = $"{currIMF[0]}{currIMF[1]}";

			if (currIMF[0] == "gg" || currIMF[0] == "dd" || currIMF[0] == "bb")
			{
				if (!singer.TryGetMappedOto($"- {currIMF[0]}{currIMF[1]}", note.tone + shift, color, out _)) currPhoneme = $"{currIMF[0].Replace('g', 'k').Replace('d', 't').Replace('b', 'p')}{currIMF[1]}";
			}
			if (currIMF[1] == "eui")
			{
				if (!singer.TryGetMappedOto("- eui", note.tone + shift, color, out _)) currPhoneme = $"{currIMF[0]}ui";
			}

			// Adjust current phoneme based on previous neighbor
			if (prevNeighbour != null && singer.TryGetMappedOto(prevNeighbour?.lyric, note.tone + shift, color, out _)) currPhoneme = $"{GetLastSoundOfAlias(prevNeighbour?.lyric)} {currPhoneme}";
			else
			{
				if (prevNeighbour == null || prevNeighbour?.lyric == "R" || prevNeighbour?.lyric == "-") currPhoneme = $"- {currPhoneme}";
				else
				{
					if (string.IsNullOrEmpty(prevNeighbour?.phoneticHint))
					{
						byte[] bytes = Encoding.Unicode.GetBytes($"{prevNeighbour?.lyric[0]}");
						int numval = Convert.ToInt32(bytes[0]) + Convert.ToInt32(bytes[1]) * (16 * 16);
						if (prevNeighbour?.lyric.Length == 1 && numval >= 44032 && numval <= 55215) prevIMF = GetIMF(prevNeighbour?.lyric);
						else return new Result
						{
							phonemes = new Phoneme[] {
								new Phoneme { phoneme = note.lyric }
							}
						};
					}
					else prevIMF = GetIMFFromHint(prevNeighbour?.phoneticHint);

					string prevConnect;

					if (!string.IsNullOrEmpty(prevIMF[2]))
					{
						if (Array.IndexOf(sonorants, prevIMF[2]) > -1)
						{
							if (singer.TryGetMappedOto($"{prevIMF[2]} {currPhoneme}", note.tone + shift, color, out _)) prevConnect = prevIMF[2];
							else prevConnect = prevIMF[2].ToUpper();
						}
						else prevConnect = "-";
					}
					else
					{
						if (prevIMF[1][0] == 'w' || prevIMF[1][0] == 'y' || prevIMF[1] == "oe") prevConnect = prevIMF[1].Remove(0, 1);
						else if (prevIMF[1] == "eui") prevConnect = "i";
						else prevConnect = prevIMF[1];
					}

					currPhoneme = $"{prevConnect} {currPhoneme}";
				}
			}

			// Return Result now if note has no batchim
			if (string.IsNullOrEmpty(currIMF[2]))
			{
				return new Result
				{
					phonemes = new Phoneme[] {
						new Phoneme { phoneme = currPhoneme }
					}
				};
			}

			// Adjust Result if note has batchim
			else
			{
				string secondPhoneme = (currIMF[1][0] == 'w' || currIMF[1][0] == 'y' || currIMF[1] == "oe" || currIMF[1] == "ui") ? currIMF[1].Remove(0, 1) : currIMF[1];

				if (nextNeighbour == null)
				{
					if (string.IsNullOrEmpty(currIMF[2])) secondPhoneme += " R";
					else
					{
						if (singer.TryGetMappedOto($"{secondPhoneme} {currIMF[2]}", note.tone + shift, color, out _)) secondPhoneme += $" {currIMF[2]}";
						else secondPhoneme += $" {currIMF[2].ToUpper()}";
					}
				}
				else if (!string.IsNullOrEmpty(currIMF[2]))
				{
					if (singer.TryGetMappedOto($"{secondPhoneme} {currIMF[2]}", note.tone + shift, color, out _)) secondPhoneme += $" {currIMF[2]}";
					else secondPhoneme += $" {currIMF[2].ToUpper()}";
				}

				int secondPosition = Math.Max(note.duration - (nextNeighbour == null ? 120 : 180), note.duration / 2);

				// Return Result
				return new Result
				{
					phonemes = new Phoneme[] {
						new Phoneme { phoneme = currPhoneme },
						new Phoneme { phoneme = secondPhoneme, position = secondPosition }
					}
				};
			}
		}
	}
}
