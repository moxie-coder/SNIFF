using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Runtime;

/*
*  The main class!
*  this is where the magic happens. sorry for the mess!
*  
*  this will keep changing as FNF's chart format
*  evolves over time. Exciting stuff
*  
*  TINY LITTLE TODO of things i wanna do:
*  - convert from MIDI!
*  - crop off the .0s after all the floats. this will
*    probably require making my own JSON deserializer :/
*  - BPM changes to and from automation data
*  - convert from playlist instead of from 1 pattern for
*    ease of use n shit
*  - read song data from inside the flp somewhere
*  - command line arguments
*  
*  pls give credit if u use or reference this or whatever.
*  <3 - MtH
*/

namespace SNIFF
{
	static class Globals
	{
		public const int VersionNumber = 7;
		public const int NoteSize = 24;
		public static ushort ppqn = 96;
		public static string name = "";
		public static float bpm = 0;
		public static List<float> bpmList = new List<float>();
		public static int needsVoices = 0; //0 = undecided, -1 = false, 1 = true
		public static string player1 = "";
		public static string player2 = "";
		public static string gfVersion = "";
		public static string stage = "";
		public static string arrowSkin = "";
	}

	public enum MIDINotes
	{
		BF_L = 48,
		BF_D = 49,
		BF_U = 50,
		BF_R = 51,
		
		BF_CAM = 53,
		EN_CAM = 54,
		
		BPM_CH = 56,
		ALT_AN = 57,
		
		EN_L = 60,
		EN_D = 61,
		EN_U = 62,
		EN_R = 63
	}

	public enum FNFNotes : int
	{
		F_L = 0,
		F_D = 1,
		F_U = 2,
		F_R = 3,

		O_L = 4,
		O_D = 5,
		O_U = 6,
		O_R = 7,

		BF_CAM = 8,
		EN_CAM = 9,
		ALT_AN = 10,
		BPM_CH = 11
	}
	

	class Program
	{
		static void ResetGlobals()
		{
			Globals.ppqn = 96;
			Globals.name = "";
			Globals.bpm = 0;
			Globals.needsVoices = 0;
			Globals.player1 = "";
			Globals.player2 = "";
			Globals.gfVersion = "";
			Globals.stage = "";
		}

		public static FLNote MakeNote(double strumTime, int noteData, float sustainLength, bool mustHitSection, float bpm)
		{
			byte velo = 0x64;
			uint noteTime = (uint)Math.Round(strumTime / MIDITimeToMillis(bpm));
			uint duration = (uint)Globals.ppqn / 4;
			uint midiPitch = 0;

			if (sustainLength > 0)
			{
				duration = (uint)(sustainLength / MIDITimeToMillis(bpm));
				if (duration < (uint)Globals.ppqn / 2)
					velo = 0x3F;
			}
			if (noteData >= (int)FNFNotes.BF_CAM)
				duration = (uint)(Globals.ppqn * 4);
			
			switch (noteData)
			{
				case (int)FNFNotes.F_L:
				case (int)FNFNotes.F_D:
				case (int)FNFNotes.F_U:
				case (int)FNFNotes.F_R:
					midiPitch = (uint)(MIDINotes.BF_L + noteData + (mustHitSection ? 0 : 12));
					break;
				case (int)FNFNotes.O_L:
				case (int)FNFNotes.O_D:
				case (int)FNFNotes.O_U:
				case (int)FNFNotes.O_R:
					midiPitch = (uint)(MIDINotes.BF_L + noteData - 4 + (mustHitSection ? 12 : 0));
					break;
				case (int)FNFNotes.BF_CAM:
					midiPitch = (uint)MIDINotes.BF_CAM;
					break;
				case (int)FNFNotes.EN_CAM:
					midiPitch = (uint)MIDINotes.EN_CAM;
					break;
				case (int)FNFNotes.ALT_AN:
					midiPitch = (uint)MIDINotes.ALT_AN;
					break;
				case (int)FNFNotes.BPM_CH:
					midiPitch = (uint)MIDINotes.BPM_CH;
					break;
				default:
					break;
			}

			return new FLNote
			{
				Time = noteTime,
				TBD = 0x4000,
				ChannelNo = 0x0000,
				Duration = duration,
				Pitch = midiPitch,
				FinePitch = 120,
				Release = 0x40,
				Flags = 0x00,
				Panning = 0x40,
				Velocity = velo,
				ModX = 0x80,
				ModY = 0x80
			};
		}

		static FLNote DefaultNote(uint time, uint duration, uint pitch)
		{
			return new FLNote
			{
				Time = time,
				TBD = 0x4000,
				ChannelNo = 0x0000,
				Duration = duration,
				Pitch = pitch,
				FinePitch = 120,
				Release = 0x40,
				Flags = 0x00,
				Panning = 0x40,
				Velocity = 0x64,
				ModX = 0x80,
				ModY = 0x80
			};
		}

		static FLNote DefaultNote()
		{
			return DefaultNote(0, (uint)Globals.ppqn / 4, 60);
		}

		static JObject DefaultSection(bool length)
		{
			JObject section = new JObject{};
			if (length)
			{
				section.Add("lengthInSteps", 16); //sigh
			}
			section.Add("mustHitSection", true);
			section.Add("sectionNotes", JArray.FromObject(new object[][] { }));
			return section; 
		}

		static byte[] FLNotesToBytes(List<FLNote> notes)
		{
			List<byte> bytes = new List<byte>();
			foreach (FLNote note in notes)
			{
				bytes.AddRange(BitConverter.GetBytes(note.Time));
				bytes.AddRange(BitConverter.GetBytes(note.TBD));
				bytes.AddRange(BitConverter.GetBytes(note.ChannelNo));
				bytes.AddRange(BitConverter.GetBytes(note.Duration));
				bytes.AddRange(BitConverter.GetBytes(note.Pitch));
				bytes.AddRange(BitConverter.GetBytes(note.FinePitch));
				bytes.Add(note.Release);
				bytes.Add(note.Flags);
				bytes.Add(note.Panning);
				bytes.Add(note.Velocity);
				bytes.Add(note.ModX);
				bytes.Add(note.ModY);
			}
			return bytes.ToArray();
		}

		static List<byte> JSONtoFL(JObject o)
		{
			Globals.ppqn = 96;
			Console.Write("Enter the PPQ value. (Default is 96, Max is 65535, 0 will set in 96) ");
			string tmppqn = Console.ReadLine();
			if (tmppqn != null) {
				Globals.ppqn = ushort.Parse(tmppqn);
			}
			if(Globals.ppqn == 0) Globals.ppqn = 96;

			List<byte> file = new List<byte>()
			{//full FLhd plus FLdt bytes
				0x46, 0x4C, 0x68, 0x64, 0x06, 0x00, 0x00, 0x00, 0x10, 0x00, 0x05, 0x00, 
				(byte)(Globals.ppqn % 256), (byte)(Globals.ppqn / 256), 0x46, 0x4C, 0x64, 0x74
			}; //then append int size of data (below) and then data itself
			List<byte> data = new List<byte>()
			{
				0xC7, 0x07, 0x31, 0x31, 0x2E, 0x31, 0x2E, 0x30, 0x00, 0x1C, 0x03, 0x41, 0x00, 0x00, 0xE0
			}; //then append size of notes and then notes themselves
			List<FLNote> notes = new List<FLNote>();

			Console.WriteLine("\nYour BPM is "+o["song"]["bpm"]);
			Console.WriteLine("\nYour speed is " + o["song"]["speed"]);
			float bpm = (float)o["song"]["bpm"];
			bool mustHitSection = true;
			var lastBPMChangeTime = new {
				u = (uint)0, f = (double)0, s = (int)0
			};

			Stopwatch sw = new Stopwatch();
			sw.Start(); int[] typeCnt = new int[2];
			int length = o["song"]["notes"].Count();

			for (int i = 0; i < length; i++)
			{
				// yes the section loop actually.
				// different kind of sex
				JObject section = (JObject)o["song"]["notes"][i];
				if(section["changeBPM"] != null && (bool)section["changeBPM"] && (float)section["bpm"] != bpm)
				{
					lastBPMChangeTime = new {
						u = (uint)(i * Globals.ppqn * 4),
						f = lastBPMChangeTime.f + ((i - lastBPMChangeTime.s) * 4.0f * (1000.0f * 60.0f / bpm)),
						s = i
					};
					bpm = (float)section["bpm"];
					notes.Add(DefaultNote(lastBPMChangeTime.u, (uint)(Globals.ppqn * 4), (uint)MIDINotes.BPM_CH));
					Console.WriteLine("BPM change found at bar " + (i + 1) + ", new BPM is " + bpm+". Keep note of this!");
				}
				if ((bool)section["mustHitSection"] != mustHitSection)
				{
					mustHitSection = !mustHitSection;
					notes.Add(DefaultNote((uint)(i * Globals.ppqn * 4), (uint)(Globals.ppqn * 4), (uint)(mustHitSection ? MIDINotes.BF_CAM : MIDINotes.EN_CAM)));
				}
				if (section["altAnim"] != null && (bool)section["altAnim"])
					notes.Add(DefaultNote((uint)(i * Globals.ppqn * 4), (uint)(Globals.ppqn * 4), (uint)MIDINotes.ALT_AN));
				
				foreach (JArray fnfNote in section["sectionNotes"])
				{
					FLNote swagNote = MakeNote((double)fnfNote[0] - lastBPMChangeTime.f, (int)fnfNote[1], (float)fnfNote[2], mustHitSection, bpm);
					swagNote.Time += lastBPMChangeTime.u;
					if (fnfNote.Last().Type == JTokenType.Boolean && fnfNote.Last().Value<bool>() == true)
						swagNote.Flags = 0x10; //set porta for alt anim Note
					switch (fnfNote.Last().Type)
                    {
						case JTokenType.Integer:
							typeCnt[0]++;
							break;
						case JTokenType.Float:
							typeCnt[1]++;
							break;
					}
					if(sw.ElapsedMilliseconds > 10)
                    {
						Console.Write("\x1b[0GInteger Cnt: " + typeCnt[0] + " Double Cnt: " + typeCnt[1] + " Total Notes Cnt: " + notes.Count + " - " + string.Format("{0:P} Done", i/length));
						sw.Restart();
					}
					notes.Add(swagNote);
				}
			}
			sw.Stop();
			Console.Write("\x1b[0GInteger Cnt: " + typeCnt[0] + " Double Cnt: " + typeCnt[1] + " Total Notes Cnt: " + notes.Count);
			Console.WriteLine("");
			byte[] nBytes = FLNotesToBytes(notes);
			// the array length lets goo
			List<byte> arrlen = new List<byte>();
			int len = nBytes.Length;
			while (len > 0)
			{
				arrlen.Add((byte)(len & 0x7f));
				len = len >> 7;
				if (len > 0)
					arrlen[arrlen.Count-1] += 0x80;
			}

			data.AddRange(arrlen.ToArray());
			data.AddRange(nBytes);
			file.AddRange(BitConverter.GetBytes(data.Count));
			file.AddRange(data);
			return file;
		}

		static void FlipNoteActor(JObject section)
		{
			for (int i = 0; i < ((JArray)section["sectionNotes"]).Count; i++)
			{
				int s = (int)section["sectionNotes"][i][1];
				if (s > 3)
					s -= 4;
				else
					s += 4;
				section["sectionNotes"][i][1] = s;
			}
		}
		static double MIDITimeToMillis(float bpm)
		{
			return (1000.0 * 60.0 / bpm / Globals.ppqn);
		}

		/* 
		 * This makes a note data event's data into a
		 * list of FLNotes
		 */
		static List<FLNote> BytesToFLNotes(byte[] b)
		{
			List<FLNote> notes = new List<FLNote>();
			int i = 0;
			while (i < b.Length)
			{
				//notes loop
				FLNote n = new FLNote
				{
					Time = BitConverter.ToUInt32(b, i),
					TBD = BitConverter.ToUInt16(b, i + 4),
					ChannelNo = BitConverter.ToUInt16(b, i + 6),
					Duration = BitConverter.ToUInt32(b, i + 8),
					Pitch = BitConverter.ToUInt32(b, i + 12),
					FinePitch = BitConverter.ToUInt16(b, i + 16),
					Release = b[i + 18],
					Flags = b[i + 19],
					Panning = b[i + 20],
					Velocity = b[i + 21],
					ModX = b[i + 22],
					ModY = b[i + 23]
				};
				notes.Add(n);
				i += Globals.NoteSize;
			}
			Console.WriteLine(notes.Count + " notes found.");
			return notes;
		}

		static JObject FLtoJSON(List<FLNote> notes, string fileName, bool addLength)
		{
			if (notes == null)
				return null;
			// after da data loop
			// let us start assembling the funk
			//Console.WriteLine("\nFirst, we gotta set up some data...");
			if (Globals.name == "") {
				Console.Write("Song name (leave here if u want set same name): ");
				Globals.name = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(Globals.name) ) {
					Globals.name = Path.GetFileNameWithoutExtension(fileName);
				}
			}
			JObject song = new JObject {
				{ "song", Globals.name }
			};
			if (Globals.bpm == 0) {
				Console.Write("BPM: ");
				Globals.bpm = float.Parse(Console.ReadLine());
			}
			else if (Globals.bpmList.Count > 0)
				Globals.bpm = Globals.bpmList[0];

			// bpm section
			song.Add("bpm", Globals.bpm);
			if (Globals.needsVoices == 0) {
				Console.Write("Use separate voices file? (y/N, default y) ");
				Globals.needsVoices = Console.ReadLine().ToLower().Trim() == "n" ? -1 : 1;
			}
			song.Add("needsVoices", Globals.needsVoices > 0);

			// player1 section
			if (Globals.player1 == "") {
				Console.Write("player1 (playable character like bf): ");
				Globals.player1 = Console.ReadLine();
				if(string.IsNullOrWhiteSpace(Globals.player1)) Globals.player1 = "bf";
			}
			song.Add("player1", Globals.player1);

			// player2 section
			if (Globals.player2 == "") {
				Console.Write("player2 (opponent character, see assets\\data\\characterList.txt): ");
				Globals.player2 = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(Globals.player2)) Globals.player2 = "dad";
			}
			song.Add("player2", Globals.player2);

			// girlfriend section
			if (Globals.gfVersion == "") {
				Console.Write("gfVersion (gf, gf-car, gf-christmas, gf-pixel): ");
				Globals.gfVersion = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(Globals.gfVersion)) Globals.gfVersion = "gf";
			}
			song.Add("gfVersion", Globals.gfVersion);

			// stage section
			if (Globals.stage == "")
			{
				Console.Write("stage (stage, halloween, philly, limo, mall, mallEvil, school, schoolEvil, tank): ");
				Globals.stage = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(Globals.stage)) Globals.stage = "stage";
			}
			song.Add("stage", Globals.stage);

			Console.Write("speed (if u leave blank, it's auto calculate based current bpm): ");
			string spd = Console.ReadLine();
			if (!string.IsNullOrWhiteSpace(spd))
				song.Add("speed", float.Parse(spd));
			else
			{
				song.Add("speed", Globals.bpm / 50);
			}
			int enableChangeBPM = 0; // 0 = no, 1 = yes, 2 = yes and use bpmList.txt

			for (int i = 0; i < notes.Count; i++)
			{
				if (notes[i].Pitch == (uint)MIDINotes.BPM_CH)
				{
					Console.Write("\nLooks like you have one or more BPM changes. ");
					if (File.Exists("bpmList.txt") && Globals.bpmList.Count == 0)
					{
						Console.Write("Do you want to use bpmList.txt?\n" +
							"(y/N, default N) ");
						if (Console.ReadLine().ToLower().Trim() == "y")
						{
							enableChangeBPM = 2;
							string[] bpmListFile = File.ReadAllLines("bpmList.txt");
							foreach (string bpmLine in bpmListFile)
							{
								bool success = float.TryParse(bpmLine, out float outBPM);
								if (success)
								{
									Globals.bpmList.Add(outBPM);
									Console.WriteLine("Added BPM " + outBPM);
								}
							}
						}
					}
					if (Globals.bpmList.Count > 0)
					{
						enableChangeBPM = 2;
						Globals.bpm = Globals.bpmList[0];
						song["bpm"] = Globals.bpm;
					}

					if (enableChangeBPM == 0)
					{
						Console.Write("Is the initial BPM of " + Globals.bpm + " correct? If so, leave the\n" +
							"following field empty. If not, please type the correct BPM.\n" +
							"BPM: ");
						string newbpm = Console.ReadLine();
						if (newbpm != "")
						{
							float daBPM = float.Parse(newbpm);
							Globals.bpm = daBPM;
							song["bpm"] = daBPM;
							Globals.bpmList.Add(daBPM);
						}
						Console.WriteLine("Selected BPM: " + Globals.bpm + "\nGreat! Keep an eye out, we'll be asking you for the new BPMs.");
						enableChangeBPM = 1;
					}
					i = notes.Count;
				}

			}
			Console.WriteLine("");

			List<JObject> sections = new List<JObject>();
			bool mustHitSection = true;
			var lastBPMChangeTime = new {
				u = (uint)0, f = (double)0, s = (int)-1
			};
			int bpmListIdx = 1;
			int totalNotes = notes.Count;
			int progress = 0;
			int gcCounter = 0;
			int sectionCnt = 0;
			List<object[]> sectionList = new List<object[]>();
			List<object> n = new List<object>(4);
			song.Add("notes", JArray.FromObject(sections));

			JToken looper = null;

			Stopwatch sw = new Stopwatch();
			sw.Start();

			//while (notes.Count > 0)
			foreach (FLNote daNote in notes)
			{
				// FLNote daNote = notes[0];
				// THE NOTE LOOP
				// this is where you have sex
				// Console.WriteLine("note FLS TIME " + daNote.Time);
				while (sectionCnt * Globals.ppqn * 4 <= daNote.Time)
				{
					if (sectionList.Count > 0)
                    {
						sections.Last()["sectionNotes"] = JToken.FromObject(sectionList.ToArray());
						looper = JToken.FromObject(sections.ToArray());
						foreach (JToken loop in looper)
						{
							((JArray)song["notes"]).Add(loop);
						}
						sections.Clear();

						/*if (sectionList.Count >= 1000000)
						{
							GC.Collect();
							gcCounter = 0;
						}*/
					} else {
						if (sw.ElapsedMilliseconds > 50)
						{
							Console.Write($"\x1b[0G{progress} / {totalNotes} Done ({progress / (double)totalNotes:P3}) Current Section: {sectionCnt}");
							sw.Restart();
						}
					}
					sections.Add(DefaultSection(addLength));
					sectionCnt++;
					//Console.WriteLine("section added");
					/*if (enableChangeBPM == 2 && lastBPMChangeTime.u > 0)
					{
						sections.Last().Add("bpm", bpm);
						sections.Last().Add("changeBPM", true);
					}*/
					sections.Last()["mustHitSection"] = mustHitSection;
					sectionList = ((JArray)sections.Last()["sectionNotes"]).ToObject<List<object[]>>();
				}

				double time = lastBPMChangeTime.f + MIDITimeToMillis(Globals.bpm) * (daNote.Time - lastBPMChangeTime.u);
				time = Math.Round(time, 6, MidpointRounding.AwayFromZero);
				//Console.WriteLine("note FNF TIME " + time);
				double sus = 0;
				//if note is 2 steps or longer, or if the velocity is lower than half
				//we actually get the sus
				if (daNote.Velocity < 0x40 || daNote.Duration >= Globals.ppqn / 2)
				{
					sus = MIDITimeToMillis(Globals.bpm) * (daNote.Duration - Globals.ppqn / 4);
					if (sus < 0) sus = 0;
				}
				switch (daNote.Pitch)
				{
					case (uint)MIDINotes.BF_CAM:
						sections.Last()["sectionNotes"] = JToken.FromObject(sectionList.ToArray());
						mustHitSection = true;
						if (sections.Last()["mustHitSection"].ToObject<bool>() != mustHitSection &&
												sectionList.Count > 0)
							FlipNoteActor(sections.Last());
						sections.Last()["mustHitSection"] = mustHitSection;
						sectionList = ((JArray)sections.Last()["sectionNotes"]).ToObject<List<object[]>>();
						break;
					case (uint)MIDINotes.EN_CAM:
						sections.Last()["sectionNotes"] = JToken.FromObject(sectionList.ToArray());
						mustHitSection = false;
						if (sections.Last()["mustHitSection"].ToObject<bool>() != mustHitSection &&
												sectionList.Count > 0)
							FlipNoteActor(sections.Last());
						sections.Last()["mustHitSection"] = mustHitSection;
						sectionList = ((JArray)sections.Last()["sectionNotes"]).ToObject<List<object[]>>();
						break;
					case (uint)MIDINotes.BPM_CH:
						if(sectionCnt == lastBPMChangeTime.s)
						{
							Console.WriteLine("\nBPM change event found on bar " + sectionCnt + ", but this section\n" +
											"already had a BPM change, so it was ignored.");
							break;
						}
						Console.WriteLine("\nBPM change event found on bar " + sectionCnt + "!");
						if (enableChangeBPM == 2 && bpmListIdx < Globals.bpmList.Count)
							Globals.bpm = Globals.bpmList[bpmListIdx++];
						else if (enableChangeBPM == 1)
						{
							Console.WriteLine("\nNew BPM (ignore if blank): ");
							string lineBPM = Console.ReadLine();
							if (lineBPM != null)
							{
								float daBPM = float.Parse(lineBPM);
								Globals.bpm = daBPM;
								Globals.bpmList.Add(daBPM);
							}
						}
							
						if (enableChangeBPM < 3 && enableChangeBPM > 0) {
							if (sections.Last().ContainsKey("changeBPM"))
								sections.Last()["bpm"] = Globals.bpm;
							else
							{
								sections.Last().Add("bpm", Globals.bpm);
								sections.Last().Add("changeBPM", true);
							}
						}
						lastBPMChangeTime = new {
							u = daNote.Time, f = time, s = sectionCnt
						};
						break;
					case (uint)MIDINotes.ALT_AN:
						sections.Last().Add("altAnim", true);
						break;
					case (uint)MIDINotes.BF_L:
						n.Add(time);
						n.Add((byte)(mustHitSection ? 0 : 4));
						n.Add(sus);
						break;
					case (uint)MIDINotes.BF_D:
						n.Add(time);
						n.Add((byte)(mustHitSection ? 1 : 5));
						n.Add(sus);
						break;
					case (uint)MIDINotes.BF_U:
						n.Add(time);
						n.Add((byte)(mustHitSection ? 2 : 6));
						n.Add(sus);
						break;
					case (uint)MIDINotes.BF_R:
						n.Add(time);
						n.Add((byte)(mustHitSection ? 3 : 7));
						n.Add(sus);
						break;
					case (uint)MIDINotes.EN_L:
						n.Add(time);
						n.Add((byte)(mustHitSection ? 4 : 0));
						n.Add(sus);
						break;
					case (uint)MIDINotes.EN_D:
						n.Add(time);
						n.Add((byte)(mustHitSection ? 5 : 1));
						n.Add(sus);
						break;
					case (uint)MIDINotes.EN_U:
						n.Add(time);
						n.Add((byte)(mustHitSection ? 6 : 2));
						n.Add(sus);
						break;
					case (uint)MIDINotes.EN_R:
						n.Add(time);
						n.Add((byte)(mustHitSection ? 7 : 3));
						n.Add(sus);
						break;
					default:
						break;
				}
				if (n != null && n.Count > 0)
				{
					// alt anim note
					if ((notes[0].Flags & 0x10) == 0x10)
						n.Add(true);
					sectionList.Add(n.ToArray());
					n.Clear();
				}

				++progress; ++gcCounter;
				if (sw.ElapsedMilliseconds > 50)
				{
					Console.Write($"\x1b[0G{progress} / {totalNotes} Done ({progress / (double)totalNotes:P3}) Current Section: {sectionCnt}");
					sw.Restart();
				}

				/*if (gcCounter >= 1000000) {
					Console.Write($"\x1b[0G{progress} / {totalNotes} Done ({progress / (double)totalNotes:P3}) Current Section: {sectionCnt}");
					GC.Collect(); gcCounter = 0;
				}*/
			}

			if (sections.Count > 0)
			{
				sections.Last()["sectionNotes"] = JToken.FromObject(sectionList.ToArray());
				looper = JToken.FromObject(sections.ToArray());
				foreach (JToken loop in looper)
				{
					((JArray)song["notes"]).Add(loop);
				}
				sections.Clear();

				/*if (sectionList.Count >= 1000000)
				{
					GC.Collect();
					gcCounter = 0;
				}*/
			}

			looper = null;
			sw.Stop();

			Console.WriteLine($"\x1b[0G{progress} / {totalNotes} Done ({progress / (double)totalNotes:P3}) Current Section: {sectionCnt}");
			//note to avoid confusion: the array of sections is called notes in json
			
			JObject file = new JObject {
					{ "song", song },
					{ "generatedBy", "SNIFF ver." + Globals.VersionNumber }
				};
			return file;
		}

		static void CollectFLPGlobals(FLFile flFile)
		{
			Globals.ppqn = flFile.ppqn;
			DwordEvent tempoEvent = (DwordEvent)flFile.FindFirstEvent(Event.EventIDs.D_PROJ_TMP);
			if (tempoEvent != null)
			{
				Globals.bpm = (uint)tempoEvent.Value / 1000.0f;
				Console.WriteLine("BPM found: " + Globals.bpm);
			}
		}

		static List<FLNote> CollectFLNotes(FLFile flFile, ushort pattern, bool strict = false)
		{
			List<FLNote> notes = new List<FLNote>();

			// if it has a project tempo it's an .flp
			if (flFile.FindFirstEvent(Event.EventIDs.D_PROJ_TMP) != null)
			{
				CollectFLPGlobals(flFile);
				bool triedPat = false;

				// get the first fpc channel and get just the notes from that,
				// if it dont exist just get them from whatever the first channel is
				ushort generator = 0;
				for (int i = 0; i < flFile.eventList.Count; i++)
				{
					if (flFile.eventList[i].ID == (byte)Event.EventIDs.A_PLUG_NAME &&
						((byte[])flFile.eventList[i].Value).SequenceEqual(new byte[] { 0x46, 0x50, 0x43, 0x00 }))
					{
						generator = (ushort)flFile.FindPrevEvent(Event.EventIDs.W_GEN_CH_NO, i).Value;
						i = flFile.eventList.Count;
						Console.WriteLine("FPC channel found at " + generator);
					}
				}

				// scrub pattern for notes from selected channel
				while (notes.Count == 0)
				{
					byte[] noteData = flFile.FindNoteDataByPatternNum(pattern);
					if (noteData != null)
					{
						notes = BytesToFLNotes(noteData);
						for (int i = 0; i < notes.Count; i++)
						{
							// remove any notes not from selected channel
							if (notes[i].ChannelNo != generator)
								notes.RemoveAt(i--);
						}
						if (notes.Count == 0 && !triedPat)
						{
							pattern = 0;
							triedPat = true;
							if (strict)
								return null;
						}
						pattern++;
					}
					else
					{
						Console.WriteLine("No notes found.");
						//Console.ReadLine();
						return null;
					}
				}
				Console.WriteLine("Notes grabbed from pattern " + (pattern - 1));
			}
			else
			{
				// if .fsc file (pattern number is ignored because there's only one pattern with id 0)
				ArrayEvent noteData = (ArrayEvent)flFile.FindFirstEvent(Event.EventIDs.A_NOTE_DATA);
				if (noteData != null)
					notes = BytesToFLNotes((byte[])noteData.Value);
				else
				{
					Console.WriteLine("No notes found.");
					return null;
				}
			}
			return notes;
		}

		//yes the main function
		[STAThread]
		static void Main(string[] args)
		{
			/* test formatting json ... thanks on stack overflow
			string str = JsonHelper.FormatJson(@"{""name"":""andy"",""age"":27,""birth"":""1997/06/21"",""score"":[{""S"":1,""A"":3,""B"":6,""C"":2,""D"":0}]}");
			Console.Write(str);

			return;
			*/
			
			// Enable ANSI Escape Sequences
			var stdout = Console.OpenStandardOutput();
			var con = new StreamWriter(stdout);
			string jsonOut = "";
			con.AutoFlush = true;
			Console.SetOut(con);
			Console.WriteLine("SiIva Note Importer For FNF (SNIFF)\nquite pungent my dear... version "+ Globals.VersionNumber +"\n");

			Console.WriteLine("Do you want to output formatted JSON? (y/N, Default N)");
			bool isFormat = Console.ReadLine().ToLower().Trim() == "y";

			Console.WriteLine("Do you need lengthInSteps Proprety for JSON? (y/N, Default Y)");
			bool addLength = !(Console.ReadLine().ToLower().Trim() == "n");

			OpenFileDialog fileBrowser = new OpenFileDialog {
				InitialDirectory = Directory.GetCurrentDirectory(),
				Filter = "FL Studio file (*.fsc, *.flp)|*.fsc;*.flp|JSON file (*.json)|*.json|All files (*.*)|*.*",
				Multiselect = true
			};
			if (args.Length == 0)
				Console.WriteLine("Select your .fsc, .flp or .json file...");
			if (args.Length > 0 || fileBrowser.ShowDialog() == DialogResult.OK)
			{
				if (args.Length == 0)
					args = fileBrowser.FileNames;
				string dir = Directory.GetCurrentDirectory();
				foreach (string fileName in args)
				{
					if (fileName.EndsWith(".json"))
					{
						Console.WriteLine("Opened JSON file: " + fileName);
						JObject o;
						try { o = JObject.Parse(File.ReadAllText(fileName)); }
						catch (Exception e)
						{
							MessageBox.Show(e.Message);
							return;
						}

						GC.Collect();
						byte[] file = JSONtoFL(o).ToArray();
						GC.Collect();

						dir = Path.GetDirectoryName(fileName);
						SaveFileDialog saveBrowser = new SaveFileDialog
						{
							InitialDirectory = dir,
							Filter = "FL Studio score file (*.fsc)|*.fsc|All files (*.*)|*.*",
							FileName = Path.GetFileNameWithoutExtension(fileName) + ".fsc",
						};
						if (saveBrowser.ShowDialog() == DialogResult.OK)
						{
							File.WriteAllBytes(saveBrowser.FileName, file);
							dir = Path.GetDirectoryName(saveBrowser.FileName);
						}
					}

					else
					{
						byte[] b = null;
						Console.WriteLine("Reading file...");
						try { b = File.ReadAllBytes(fileName); }
						catch (Exception e)
						{
							MessageBox.Show(e.Message);
							return;
						}
						if (b == null || b.Length < 4)
							return;

						Console.WriteLine("Reading Done");
						FLFile flFile = new FLFile(b);

						ushort[] patterns = new ushort[] { 0, 0, 0 };
						string[] diffnames = new string[] { "easy", "normal", "hard" };
						bool diffs = false;
						for (int j = 0; j < diffnames.Length; j++)
						{
							patterns[j] = flFile.FindPatternNumByName(diffnames[j]);
							if (patterns[j] != 0)
							{
								diffs = true;
								Console.WriteLine("Found \"" + diffnames[j] + "\" pattern!");
							}
							else
								Console.WriteLine("No pattern named \"" + diffnames[j] + "\".");
						}
						Console.WriteLine();
						if (!diffs)
						{
							WordEvent curPat = (WordEvent)flFile.FindFirstEvent(Event.EventIDs.W_CUR_PAT);
							if (curPat != null)
								patterns[0] = (ushort)curPat.Value;
							else
								patterns[0] = 1;
						}

						for (int i = 0; i < patterns.Length; i++)
						{
							GC.Collect();
							if (patterns[i] != 0)
							{
								if (diffs)
									Console.WriteLine("Current difficulty: " + diffnames[i]);
								JObject file = FLtoJSON(CollectFLNotes(flFile, patterns[i], diffs), fileName, addLength);
								if (file != null)
								{
									SaveFileDialog saveBrowser = new SaveFileDialog
									{
										InitialDirectory = dir,
										Filter = "JSON File (*.json)|*.json|All files (*.*)|*.*",
										FileName = Path.GetFileNameWithoutExtension(fileName),
									};
									if (diffs && diffnames[i] != "normal")
										saveBrowser.FileName += "-" + diffnames[i];
									saveBrowser.FileName += ".json";
									if (saveBrowser.ShowDialog() == DialogResult.OK)
									{
										jsonOut = file.ToString(Formatting.None);
										if (isFormat) Console.WriteLine("Formatting json....");
										File.WriteAllText(saveBrowser.FileName, isFormat ? JsonHelper.FormatJson(jsonOut) : jsonOut);
										dir = Path.GetDirectoryName(saveBrowser.FileName);
									}
								}
							}
						}
						ResetGlobals();
					}
					GC.Collect();
				}

				Console.WriteLine("Press any key to close...");
				Console.ReadKey();
				return;
			}
			else
				Console.WriteLine("Dialog closed");
		}
	}
}
