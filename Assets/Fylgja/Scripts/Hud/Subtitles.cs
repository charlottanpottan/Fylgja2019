using System;
using UnityEngine;
using System.Collections;
using System.Globalization;
using UnityEngine.UI;



public class Subtitles : MonoBehaviour
{
    public Text text;
	public int textSize = 30;
	public float delayBetweenSubtitles = 0.2f;
	public float delayAfterSubtitle = 0.5f;
	float closedTextAtTime;
	string nextTitle = "";
	float nextTitleTime;
	private float showedTitleAtTime;
	enum Command
	{
		WaitTime,
		VideoTime
	}

	private Command nextCommand;
	bool titleEnabled = false;
	bool shouldBeVisible = false;
	private MoviePlayerToCamera moviePlayerToCamera;

	
	void Start()
	{
		text.text = string.Empty;
		text.transform.parent.gameObject.SetActive(false);
		moviePlayerToCamera = FindObjectOfType<MoviePlayerToCamera>();
	}

	bool HasShownTitleForLongTime(float referenceTime)
	{
		const float maximumTimeToShowTitleWithoutNext = 7.0f;
		return referenceTime >= showedTitleAtTime + maximumTimeToShowTitleWithoutNext;
	}
	
	bool IsLongTimeToNextLineOrAtEnd(float referenceTime)
	{
		const float longTimeUntilNextSubtitle = 10.0f;
		return nextTitleTime - referenceTime > longTimeUntilNextSubtitle || nextTitle.Length == 0;
	}

	bool DoesLogicallyHaveText()
	{
		return text.text.Length != 0;
	}

	bool IsLogicallyShowingText()
	{
		return titleEnabled && DoesLogicallyHaveText();
	}


	bool IsLongTimeSinceWeLogicallyClosedLine()
	{
		return Time.time > closedTextAtTime + delayAfterSubtitle;
	}

	bool IsShowingBorderButNoLogicalText()
	{
		return text.text == string.Empty && titleEnabled;
	}
	void CloseSubtitleIfLongGapToNextLine(float referenceTime)
	{
		if (IsLogicallyShowingText() && HasShownTitleForLongTime(referenceTime) && IsLongTimeToNextLineOrAtEnd(referenceTime) )
		{
			Debug.Log("Subtitle has been shown too long. closing.");
			CloseSubtitle();
		}
	}

	bool IsThereUpcomingText()
	{
		return nextTitle.Length != 0;
	}

	bool IsNextLineComingUpRealSoon(float referenceTime)
	{
		return referenceTime >= nextTitleTime - delayBetweenSubtitles;
	}

	bool IsTimeForNextLine(float referenceTime)
	{
		return referenceTime >= nextTitleTime;
	}

	void CheckUpcomingText(float referenceTime)
	{
		if (DoesLogicallyHaveText() && IsNextLineComingUpRealSoon(referenceTime))
		{
			Debug.Log($"nextTitle forced subtitle stop of line:{text.text}");
			CloseSubtitle();
		}

		if (IsThereUpcomingText() && IsTimeForNextLine(referenceTime))
		{
			Debug.Log($"nextTitle moved on to next subtitle {nextTitle} because reference time {referenceTime} > title time: {nextTitleTime}");
			var nextTitleToShow = nextTitle;
			nextTitle = string.Empty;
			OnSubtitleStart(nextTitleToShow);
		}
	}
	
	void Update()
	{
		var referenceTime = Time.time;
		if (nextCommand == Command.VideoTime)
		{
			var movieTimeInSeconds = moviePlayerToCamera.videoPlayer.time;
			referenceTime = (float) movieTimeInSeconds;
		}
		
		if (IsShowingBorderButNoLogicalText() && IsLongTimeSinceWeLogicallyClosedLine())
		{
			Debug.Log("Subtitle timed out subtitle");
			titleEnabled = false;
			text.transform.parent.gameObject.SetActive(false);
		}

		CloseSubtitleIfLongGapToNextLine(referenceTime);
		
		if (IsThereUpcomingText())
		{
			CheckUpcomingText(referenceTime);
		}
	}

	public bool ShouldBeVisible
	{
		set
		{
			shouldBeVisible = value;
		}
	}

	bool ParseCommandIfFound(string title, out string nextText)
	{
		var escapeIndex = title.IndexOf('%');

		nextText = title;

		if (escapeIndex != -1)
		{
			var endEscapeIndex = title.IndexOf(' ', escapeIndex + 1);
			DebugUtilities.Assert(endEscapeIndex != -1, "Illegal formatting:" + title);
			var escapeCode = title.Substring(escapeIndex + 1, endEscapeIndex - escapeIndex);
			var textBefore = title.Substring(0, escapeIndex).TrimEnd();
			var textAfter = title.Substring(endEscapeIndex + 1);
			var parameters = escapeCode.Substring(1);

			nextText = textBefore;
			
			switch (escapeCode[0])
			{
				case 'w':
				{
					var waitTime = float.Parse(parameters);
					nextTitle = textAfter;
					nextTitleTime = Time.time + waitTime;
					nextCommand = Command.WaitTime;
				
					Debug.Log("Waiting time:" + waitTime + " remaining:" + nextTitle + " showing:" + textBefore);
				   
					nextText = textAfter;
					break;
				}
				case 't':
				{
					// Couldn't get TimeSpan.ParseExact to work, so added custom parsing.
					var separator = parameters.IndexOf(':');
					if (separator == -1)
					{
						Debug.LogError($"wrong time format '{parameters}'");
					}

					var minutesString = parameters.Substring(0, separator);
					var secondsString = parameters.Substring(separator + 1);
					var minutes = int.Parse(minutesString);
					var seconds = int.Parse(secondsString);
					
					nextTitleTime = minutes * 60 + seconds;
					nextTitle = textAfter;
					nextCommand = Command.VideoTime;
					break;
				}
			}
		}

		return escapeIndex != -1;
	}
	
	public string ParseUpcomingCommandsAndReturnStringToDisplay(string title)
	{
		ParseCommandIfFound(title, out title);
		
		if (title.Length > 80)
		{
			var breakChars = new[]{'.', ',', ' ', ':', '-'};
			var index = title.IndexOfAny(breakChars, title.Length / 2);
			return title.Substring(0, index + 1) + "\n" + title.Substring(index + 1);
		}
		else
		{
			return title;
		}
	}
	
	public void OnSubtitleStart(string title)
	{
		Debug.Log("Subtitle start: (before)" + title);
		if (title == string.Empty)
		{
			return;
		}
		text.text = ParseUpcomingCommandsAndReturnStringToDisplay(title);
		if (text.text.Length == 0)
		{
			return;
		}
		Debug.Log($"Subtitle start: (after) {{text.text}} next subtitle is at {nextTitleTime} with '{nextTitle}'");
		if (shouldBeVisible)
		{
			titleEnabled = true;
			text.transform.parent.gameObject.SetActive(true);
		}

		showedTitleAtTime = Time.time;
	}

	void CloseSubtitle()
	{
		Debug.Log("Subtitle stop:");
		text.text = string.Empty;
		closedTextAtTime = Time.time;
	}
	
	public void OnSubtitleStop()
	{
		CloseSubtitle();
		nextTitle = string.Empty;
		nextTitleTime = 0;
	}
}
