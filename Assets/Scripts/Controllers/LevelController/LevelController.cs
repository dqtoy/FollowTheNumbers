﻿using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class LevelController : MonoBehaviour
{
	#region Variables
    public static LevelController Instance;

    [Header("Unity Stuffs")]
    public GameObject cardPrefab;
	public Transform tableTransform;
	public Sprite openCardSprite;
	public Sprite closeCardSprite;
	public LevelUIController UICont;

	List<GameObject> cardGoList;

    [Header("Level Handler")]
    public int levelNo;
    public float timePassed;
	public int nextNumber;
	public int wrongTries;
	public float starPercent;
	public int tableSize = 49;
	public Level currLevel;
	public LevelMode levelMode;
	public bool levelCompleted;

	// easy max 10, medium max 16, hard max 25
	float[] tryUpperLimitMultiplier  = new float[]{0.9f, 0.9f, 1.0f};
	float[] tryLowerLimitMultiplier  = new float[]{0.1f, 0.2f, 0.3f};
	float[] timeUpperLimitMultiplier = new float[]{1.3f, 1.5f, 2.0f};
	float[] timeLowerLimitMultiplier = new float[]{0.4f, 0.7f, 1.1f};

	public int indexStarLines;
    public float fillSpeed = .7f;
	public float[] starPercents = { 0.333f, 0.666f, 1f };

	public bool levelStarted;
	public bool levelPaused;
	public bool levelFinished;
	public bool showingAllCards;

    public float timeFlowFactor;

    Action changeSuccedScreenMethod;

	public List<Level> levels;
	#endregion

    // Use this for initialization
    void Start()
	{
		if (Instance == null)
            Instance = this;

		cardGoList = new List<GameObject>();

		changeSuccedScreenMethod = this.ChangeSucceedScreenState;

		levelNo   = DataTransfer.levelNo;
		levelMode = DataTransfer.levelMode;

		levels = LevelPickerController.GetLevels(levelMode);

        SetupLevel();
    }
		
	public void SetupLevel(bool restart = false)
    {
		if(restart == true)
			levelNo--;

		// Destroy old cards
		foreach(GameObject go in cardGoList)
			Destroy(go);

		cardGoList.Clear();

		// Set up variables
		timePassed = 0;
		wrongTries = 0;
		nextNumber = 1;
		timeFlowFactor = 1f;
		levelStarted = false;
		levelFinished = false;
		levelCompleted = false;
		UICont.lastOpenedCard = null;
		levelNo++;

		currLevel = levels[levelNo - 1];

		UICont.SetLevelText(levelNo);
		UICont.SetupUI();

        List<int> numbers = setNumbersList();
		for (int cardNo = 1; cardNo <= tableSize; cardNo++)
        {
			Transform card = Instantiate(cardPrefab, tableTransform).transform;
			cardGoList.Add(card.gameObject);

            if (currLevel.design.Contains(cardNo) == false)
            {
				Card cardComponenet = card.GetComponent<Card>();
				cardComponenet.active = false;
            }
            else
            {
				int index = Random.Range(0, numbers.Count);
				Card cardComponenet = card.GetComponent<Card>();

				cardComponenet.active = true;
				cardComponenet.cardNumber = numbers[index];

                numbers.RemoveAt(index);
            }
        }
    }

	void OnApplicationQuit()
	{
		ProgressController.SetRemainingTime(DataTransfer.remainingTime);
	}

	void OnApplicationPause()
	{
		ProgressController.SetRemainingTime(DataTransfer.remainingTime);
	}

	void Update()
	{
		UICont.UpdateInfo();

		if (currLevel == null || levelCompleted == true)
			return;
		
		if(levelStarted == true && levelPaused == false)
			timePassed += Time.deltaTime;

		if(showingAllCards == true)
		{
			if(DataTransfer.remainingTime - Time.deltaTime * timeFlowFactor <= 0f)
			{
				DataTransfer.remainingTime = 0f;
				RestoreCards();
			}
			else
				DataTransfer.remainingTime -= Time.deltaTime * timeFlowFactor;
		}

		if((levelMode == LevelMode.NO_MISTAKE && levelFinished == true) ||
			nextNumber > currLevel.totalCardCount)
		{
			nextNumber--;
			levelCompleted = true;
			StartCoroutine(ExecuteAfterTime(1.0f, changeSuccedScreenMethod));
		}
	}

    /// <summary>
    /// show or hide the screen
    /// </summary>
    public void ChangeSucceedScreenState()
    {
        // Yapılacak hata sayısı için üst ve alt limit belirliyorum ve buna göre oranlıyorum.
        // Aynı şekilde süre içinde. Mantık bu kadar basit.
        // tableSize * factor -> factor sayesinde ayar yapabiliriz. Duruma göre belki
        // level sayısınıda faktör olarak ekleriz.

		float starPercentForTries;
		float wrongTryUpperLimit = currLevel.totalCardCount * tryUpperLimitMultiplier[((int) currLevel.difficulty) - 1];
		float wrongTryLowerLimit = currLevel.totalCardCount * tryLowerLimitMultiplier[((int) currLevel.difficulty) - 1];
		starPercentForTries = (wrongTryUpperLimit - wrongTries) / (wrongTryUpperLimit - wrongTryLowerLimit);
		starPercentForTries = Mathf.Clamp01(starPercentForTries);

		float starPercentForTime;
		float passedTimeUpperLimit = currLevel.totalCardCount * timeUpperLimitMultiplier[((int) currLevel.difficulty) - 1];
		float passedTimeLowerLimit = currLevel.totalCardCount * timeLowerLimitMultiplier[((int) currLevel.difficulty) - 1];
		starPercentForTime = (passedTimeUpperLimit - timePassed) / (passedTimeUpperLimit - passedTimeLowerLimit);
		starPercentForTime = Mathf.Clamp01(starPercentForTime);

		float starPercentForBestCount;
		float bestCountUpperLimit = currLevel.totalCardCount * 1f;
		float bestCountLowerLimit = currLevel.totalCardCount * 0f;
		starPercentForBestCount = (nextNumber - bestCountLowerLimit) / (bestCountUpperLimit - bestCountLowerLimit);
		starPercentForBestCount = Mathf.Clamp01(starPercentForBestCount);


		if(levelMode == LevelMode.CLASSIC)
			starPercent = starPercentForTime * 0.5f + starPercentForTries * 0.5f;
		else if(levelMode == LevelMode.DO_NOT_FORGET)
			starPercent = starPercentForTries;
		else if(levelMode == LevelMode.NO_MISTAKE)
			starPercent = starPercentForBestCount;

		starPercent = ClampStarPercent(starPercent);

		int reward = 0;
		if(starPercent != 0f && ProgressController.IsRewarded(levelMode, levelNo) == false)
		{
			if(starPercent <= starPercents[0])
				reward = 1;
			else if(starPercent <= starPercents[1])
				reward = 3;
			else if(starPercent <= starPercents[2])
				reward = 5;

			DataTransfer.remainingTime += reward;
		}

		UICont.SetRewardText(reward);

		SaveProgress();
		UICont.ToggleSucceedScreen();
		StartCoroutine(UICont.FillStarImage(starPercent));
    }

	float ClampStarPercent(float starPer)
	{
		if(starPercent == 0.0f)
			return 0.0f;
		
		for(int i = 0; i < starPercents.Length; i++)
		{
			if(starPer <= starPercents[i])
				return starPercents[i];
		}

		Debug.LogError("ClampStarPercent() -- If we here, we have a problem with starPercents");
		return 0.0f;
	}

	public void ShowAllCards()
	{
		if(levelPaused == true)
			return;

		if(DataTransfer.remainingTime <= 0f)
		{
			UICont.ToggleRewardScreen(true);
			return;
		}

		showingAllCards = true;

		foreach(GameObject go in cardGoList)
		{
			Card card = go.GetComponent<Card>();
			if(card.cardNumber == 0)
				continue;

			LevelDifficulty currDif = currLevel.difficulty;

			// If card is not cleared show anyway
			if(card.cardCleared == false)
				card.OpenCard();
			// If difficulty is easy open card.
			else if(currDif == LevelDifficulty.EASY)
				card.OpenCard();
			// If difficulty is medium open card but transparent.
			else if(currDif == LevelDifficulty.MEDIUM)
				card.OpenCardTransparent();
			// If difficulty is hard close card.
			else if(currDif == LevelDifficulty.HARD)
				card.CloseCard();
		}
	}

	public void RestoreCards()
	{
		showingAllCards = false;

		foreach(GameObject go in cardGoList)
		{
			Card card = go.GetComponent<Card>();
			if(card.cardNumber == 0)
				continue;

			if(card.cardCleared == true)
				card.OpenCard();
			else
				card.CloseCard();
		}

		timeFlowFactor += .3f;
	}

    IEnumerator ExecuteAfterTime(float time, Action method)
	{
		yield return new WaitForSeconds(time);
		method();
	}

	void SaveProgress()
	{
		PlayerProgress currLevelProgress = new PlayerProgress(
			levelMode,
			levelNo,
			starPercent,
			timePassed,
			wrongTries,
			nextNumber,
			true,
			false
		);

		ProgressController.SaveProgress(currLevelProgress);



		// If there is no level or we could not get one star, do not unlock next level.
		if(levelNo == levels.Count)
		{
			UICont.DisableNextButton();
			return;
		}

		PlayerProgress nextLevelProgress = ProgressController.GetProgress(levelMode, levelNo + 1);

		if(starPercent < starPercents[0])
		{
			if(nextLevelProgress.locked == true)
			{
				UICont.DisableNextButton();
				return;
			}
		}

		nextLevelProgress.locked = false;

		ProgressController.SaveProgress(nextLevelProgress);
	}

	/// <summary>
	/// we need the number list to make a random list of numbers
	/// </summary>
	List<int> setNumbersList()
	{
		List<int> numbers = new List<int>();
		for (int i = 1; i <= currLevel.totalCardCount; i++)
			numbers.Add(i);

		return numbers;
	}
}
