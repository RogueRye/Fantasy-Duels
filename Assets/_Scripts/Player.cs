﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class Player : MonoBehaviour {

    #region Public Fields

    public bool isPlayerA = true;
    public Player opponent;
    public int lifePoints;
    public int maxAP = 10;
    public Deck deck;
    public int startingHandSize = 5;
    public Transform handObj;
    public CardHolder[] cardTypes;
    public Transform optionsMenu;
    public Transform combatOptionsMenu;
    public RectTransform deckSpot;
    public RectTransform gySpot; 
    public GameObject TextPanel;
    public TMP_Text closeUp;
    public TurnPhase currentPhase;
    public bool hasAI = false;
    #endregion

    #region Hidden Public
    [HideInInspector]
    public List<CardHolder> hand = new List<CardHolder>();
    [HideInInspector]
    public CardHolder selectedCard;
    [HideInInspector]
    public Slot[,] field;
    [HideInInspector]
    public Slot selectedSlot;
    [HideInInspector]
    public List<CreatureCard> creaturesOnField = new List<CreatureCard>();
    #endregion

    #region Private References
    Stack<CardHolder> deckStack = new Stack<CardHolder>();
    Stack<CardHolder> gyStack = new Stack<CardHolder>();
    HorizontalLayoutGroup layout;
    
    #endregion

    #region Private Values

    int curLifePoints;
    int currentAP;
    int currentMaxAp;
    #endregion

    #region Inputs
    bool input1;
    bool input2;
    #endregion


    virtual protected void Start()
    {

        layout = handObj.GetComponent<HorizontalLayoutGroup>();
        deck.Init();
        curLifePoints = lifePoints;
        currentMaxAp = (isPlayerA) ? 0 : 1;
        field = (isPlayerA) ? Board.fieldA : Board.fieldB;

        foreach(var slot in field)
        {
            slot.Unlock();
            slot.owner = this;
        }
        
        deck.Shuffle();

        StackDeck();
      
        DrawCard(startingHandSize);

        if (isPlayerA)
        {
            StartTurn();
        }
        else
        {
            currentPhase = TurnPhase.NotTurnMyTurn;
        }
    }
    #region Turn State Calls
    public void StartTurn()
    {
        if(currentPhase != TurnPhase.Start)
        {
            StartCoroutine(TurnStart());
        }
    }

    public void CastCard()
    {
        if (selectedCard.thisCard.castCost <= GetAP())
        {
            StartCoroutine(CastingCard());
        }
        else
        {
            Debug.Log("Can't Cast card");
            DelselectCard(true);
            currentPhase = TurnPhase.Main;
        }

    }

    public void StartAttackPhase()
    {
        DelselectCard();
        if (currentPhase == TurnPhase.Combat || currentPhase == TurnPhase.Attacking)
        {
            StopAttackPhase();
            return;
        }

        currentPhase = TurnPhase.Combat;

        CamBehaviour.singleton.SwitchToPosition(1);
    }

    public void Attack()
    {
        if (selectedCard != null)
        {
            var temp = selectedCard as CreatureCard;
            if (temp.canAttack)
                StartCoroutine(WaitForAttackTarget(temp));
            else
                Debug.Log("cant attack");
        }
    }

    public void StopAttackPhase()
    {
        currentPhase = TurnPhase.Main;
        CamBehaviour.singleton.SwitchToPosition(0);
    }

    public void EndTurn()
    {
        currentPhase = TurnPhase.NotTurnMyTurn;
        opponent.StartTurn();
    }

    #endregion

    #region Turn State Coroutines

    private IEnumerator TurnStart()
    {
        currentPhase = TurnPhase.Start;
        DrawCard(1);

        foreach (var card in creaturesOnField)
            card.canAttack = true;

        if (currentMaxAp < maxAP)
        {
            currentMaxAp++;
        }
        currentAP = currentMaxAp;

        yield return null;
        StartCoroutine(PickingCard());
    }

    private IEnumerator PickingCard()
    {
        currentPhase = TurnPhase.Main;
        while(selectedCard == null)
        {

            yield return null;
        }        
    }

    private IEnumerator CastingCard()
    {  
        currentPhase = TurnPhase.Casting;
        if(optionsMenu != null)
            optionsMenu.gameObject.SetActive(false);

        if (selectedCard is CreatureCard)
        {
            while (selectedSlot == null)
            {
                if (selectedCard == null)
                {
                    break;
                }

                //Draw line
                yield return null;
            }
          
            if (selectedCard != null && selectedSlot.currentCard == null)
            {
                selectedCard.Cast(selectedSlot);
                creaturesOnField.Add((CreatureCard)selectedCard);
                selectedSlot.Block();               
                DelselectCard();
                selectedSlot = null;
            }
        }
        else if(selectedCard is SpellCard)
        {
            //Do different Things
        }

       currentPhase = TurnPhase.Main;
    }

    private IEnumerator WaitForAttackTarget(CreatureCard attackingCreature)
    {
        currentPhase = TurnPhase.Attacking;
        combatOptionsMenu.gameObject.SetActive(false);
        foreach (var slot in field)
            slot.Lock();            
        
        foreach(var slot in opponent.field)
        {
            //check if its in creature's attack pattern. use switch statement? maybe not since they can have multiple patterns
            if (slot.currentCard != null)
                slot.Unlock();
            else
                slot.Lock();
        }

        while (selectedSlot == null)
        {
            if (selectedCard == null || currentPhase == TurnPhase.Main)
                break;
            yield return null;
        }
        if (currentPhase != TurnPhase.Main)
        {
            if (selectedCard != null)
            {
                attackingCreature.Attack(selectedSlot.currentCard);
                Debug.Log("Attack now!");
            }          
            currentPhase = TurnPhase.Combat;
        }
        DelselectCard();
        selectedSlot = null;
        foreach (var slot in field)
            slot.Unlock();
        foreach (var slot in opponent.field)
            slot.Lock();
    }

    #endregion

    virtual protected void Update()
    {

        if (currentPhase == TurnPhase.NotTurnMyTurn)
            return;

   
        if (!hasAI)
        {
            GetInput();
            if (input2)
            {
                DelselectCard(true);
            }
        }
    }

    virtual protected void GetInput()
    {
#if UNITY_STANDALONE

        input1 = Input.GetMouseButtonDown(0);
        input2 = Input.GetMouseButtonDown(1);



#elif UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR

        if (Input.touchCount > 1)
        {
            // input1 = Input.GetTouch(0).phase == TouchPhase.Began;
            input2 = Input.GetTouch(1).phase == TouchPhase.Began;
        }
#endif
    }

    /// <summary>
    /// Add card from deck to hand
    /// </summary>
    /// <param name="numOfCards">Number of cards to draw</param>
    public void DrawCard(int numOfCards)
    {  
        for (int i = 0; i < numOfCards; i++)
        {
            if (deckStack.Count == 0)
            {
                Debug.Log("out of cards");
                return;
            }
          
            var newCard = deckStack.Pop();

            //Only reveal card if its the players and not the AI
            if(isPlayerA)
                newCard.ToggleVisible(true);

            if (hand.Count > 7)
            {
                DiscardCard(newCard);
                              
            }
            else
            {
                hand.Add(newCard);
                newCard.transform.SetParent(handObj);                
                newCard.transform.localRotation = Quaternion.Euler(Vector3.up * 180);
                newCard.transform.localPosition = Vector3.zero - (Vector3.forward * .01f * hand.Count);

                if (hand.Count != 0 && hand.Count > 6)
                    layout.spacing -= (.7f);
                else if (hand.Count < 6)
                    layout.spacing = .7f;
            }
        }   
    }

    /// <summary>
    /// Send Card to the graveyard and add it to the GY stack/list
    /// </summary>
    /// <param name="card">card to discard</param>
     public void DiscardCard(CardHolder card)
    {
        //Unselect it, add to to stack, reset transform to the graveyard
        DelselectCard();
        gyStack.Push(card);
        card.transform.SetParent(gySpot);
        var pos = gySpot.position + (Vector3.up * (gyStack.Count * 0.075f));
        card.transform.position = pos;
        card.transform.rotation = gySpot.rotation;

        //Remove card from hang
        if (hand.Contains(card))
        {
            hand.Remove(card);
        }
        if (creaturesOnField.Contains((CreatureCard)card))
        {
            creaturesOnField.Remove((CreatureCard)card);
        }
        
    }

    /// <summary>
    /// Default override to discard the currently selected card. Used for UI buttons/Testing
    /// </summary>
    public void DiscardCard()
    {       
        if (selectedCard != null)
        {
            DiscardCard(selectedCard);
        }
    }

    /// <summary>
    /// Pick the card, show options menu, move card position to indicate selection
    /// </summary>
    /// <param name="card">card to select</param>
    public void SelectCard(CardHolder card)
    {
        if (hand.Contains(card) && currentPhase == TurnPhase.Main)
        {
            DelselectCard();
            selectedCard = card;
            if (optionsMenu != null)
            {
                optionsMenu.gameObject.SetActive(true);
                optionsMenu.gameObject.transform.position = Input.mousePosition;
            }

            selectedCard.transform.localPosition += (Vector3.up * 2.5f);
        }
        else if(currentPhase == TurnPhase.Combat && card is CreatureCard)
        {
            DelselectCard();
            selectedCard = card;
            if (combatOptionsMenu != null)
            {
                combatOptionsMenu.gameObject.SetActive(true);
                combatOptionsMenu.gameObject.transform.position = Input.mousePosition;
            }
            // Do some attacking things
        }
    }


    /// <summary>
    /// move a selected card back and hide options menu when the card is no longer selected
    /// </summary>
    public void DelselectCard(bool needsDisplacing = false)
    {
        if (selectedCard == null)
            return;
        if(needsDisplacing)
            selectedCard.transform.localPosition -= (Vector3.up * 2.5f);

        selectedCard = null;
        if (optionsMenu != null)
        {
            optionsMenu.gameObject.SetActive(false);
            combatOptionsMenu.gameObject.SetActive(false);
        }

    }

    /// <summary>
    /// Card text can be hard to read, this will show it in an easier to read panel
    /// </summary>
    public void ShowText()
    {
        
        if (selectedCard == null)
            return;

        if(TextPanel != null)
            TextPanel.SetActive(true);
       
        closeUp.text = selectedCard.thisCard.description;
    }

    public void TakeDamage(int power)
    {
        curLifePoints -= power;
    }

    public int GetLifePoints()
    {
        return curLifePoints;
    }

    public void SpendAP(int amount)
    {
        currentAP -= amount;
    }

    public int GetAP()
    {
        return currentAP;
    }

    private void StackDeck()
    {
        for (int i = 0; i < deck.deckSize; i++)
        {

            var cardToPlace = deck.mDeck.Pop();

            var pos = deckSpot.position + (Vector3.up * (i * 0.075f));
            var deckCard = Instantiate(cardTypes[(int)cardToPlace.type], pos, deckSpot.rotation, deckSpot);

            deckCard.Init(cardToPlace, this, opponent);
            deckCard.ToggleVisible(false);
            deckStack.Push(deckCard);
        }
    }
   
}

public enum TurnPhase
{
    Start,
    Main,
    Casting,
    Combat,
    Attacking,
    End,
    NotTurnMyTurn
}
