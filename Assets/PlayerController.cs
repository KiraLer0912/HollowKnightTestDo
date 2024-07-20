using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    public int health;
    public float moveSpeed;
    public float jumpSpeed;
    public int jumpLeft;
    public Vector2 climbJumpForce;
    public float fallSpeed;
    public float sprintSpeed;
    public float sprintTime;
    public float sprintInterval;
    public float attackInterval;

    public Color invulnerableColor;
    public Vector2 hurtRecoil;
    public float hurtTime;
    public float hurtRecoverTime;
    public Vector2 deathRecoil;
    public float deathDelay;

    public Vector2 attackUpRecoil;
    public Vector2 attackForwardRecoil;
    public Vector2 attackDownRecoil;

    public GameObject attackUpEffect;
    public GameObject attackForwardEffect;
    public GameObject attackDownEffect;

    private bool _isGrounded;
    private bool _isClimb;
    private bool _isSprintable;
    private bool _isSprintReset;
    private bool _isInputEnabled;
    private bool _isFalling;
    private bool _isAttackable;

    private float _climbJumpDelay = 0.2f;
    private float _attackEffectLifeTime = 0.05f;

    private Animator _animator;
    private Rigidbody2D _rigidbody;
    private Transform _transform;
    private SpriteRenderer _spriteRenderer;
    private BoxCollider2D _boxCollider;

    // Start is called before the first frame update
    private void Start()
    {
        _isInputEnabled = true;
        _isSprintReset = true;
        _isAttackable = true;

        _animator = gameObject.GetComponent<Animator>();
        _rigidbody = gameObject.GetComponent<Rigidbody2D>();
        _transform = gameObject.GetComponent<Transform>();
        _spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        _boxCollider = gameObject.GetComponent<BoxCollider2D>();
    }

    // Update is called once per frame
    private void Update()
    {
        updatePlayerState();
        if (_isInputEnabled)
        {
            move();
            jumpControl();
            fallControl();
            sprintControl();
            attackControl();
        }
    }

    //This method is called when a collider first makes contact with another collider.
    //ItÅfs useful for detecting the start of a collision
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // enter climb state. collision.collider.tag checks if the tag of the collider involved in the collision is "Wall"
        if (collision.collider.tag == "Wall" && !_isGrounded)
        {
            //change gravity to 0
            _rigidbody.gravityScale = 0;

            //assign new and fixed downward velocity
            Vector2 newVelocity;
            newVelocity.x = 0;
            newVelocity.y = -2;

            //replace old velocity with new velocity
            _rigidbody.velocity = newVelocity;

            //set character to be able to climb in code and in animator window
            _isClimb = true;
            _animator.SetBool("IsClimb", true);

            //set character to be able to sprint
            _isSprintable = true;
        }
    }

    //This method is called every frame while a collider is in contact with another collider.
    //ItÅfs useful for continuous collision detection and ensuring player remains in climbing state.
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.tag == "Wall" && _isFalling && !_isClimb)
        {
            OnCollisionEnter2D(collision);
        }
    }

    public void hurt(int damage)
    {
        //set character to a layer named "PlayerInvulnerable" to avoid being constantly attacked every frame
        gameObject.layer = LayerMask.NameToLayer("PlayerInvulnerable");

        //update new health and Math.Max() made sure that the health wont go below 0
        health = Math.Max(health - damage, 0);

        //check for death and exit function if health point = 0
        if (health == 0)
        {
            die();
            return;
        }

        // enter invulnerable state
        _animator.SetTrigger("IsHurt");

        // stop player movement
        Vector2 newVelocity;
        newVelocity.x = 0;
        newVelocity.y = 0;
        _rigidbody.velocity = newVelocity;

        // visual effect
        _spriteRenderer.color = invulnerableColor;

        // death recoil
        Vector2 newForce;
        newForce.x = -_transform.localScale.x * hurtRecoil.x;
        newForce.y = hurtRecoil.y;
        _rigidbody.AddForce(newForce, ForceMode2D.Impulse);

        //set player unable to move when is hurt
        _isInputEnabled = false;

        //start coroutine of recoverFroomHurtCorountine()
        StartCoroutine(recoverFromHurtCoroutine());
    }

    private IEnumerator recoverFromHurtCoroutine()
    {
        //pauses coroutine for the duration of hurtTime so that the code wont straight set _isInputEnabled to true
        yield return new WaitForSeconds(hurtTime);
        //after a delay, character will be able to move
        _isInputEnabled = true;
        //pauses corountine for the duration of hurtRecoverTime so that the code wont straight reset sprite color
        yield return new WaitForSeconds(hurtRecoverTime);
        //reset sprite color and character layer to original 
        _spriteRenderer.color = Color.white;
        gameObject.layer = LayerMask.NameToLayer("Player");
    }

    //This method is called when a collider stops touching another collider.
    //ItÅfs useful for detecting the end of a collision.
    private void OnCollisionExit2D(Collision2D collision)
    {
        // exit climb state
        if (collision.collider.tag == "Wall")
        {
            //set character to be unable to climb after leaving the wall in code and in animator window
            _isClimb = false;
            _animator.SetBool("IsClimb", false);

            //reset gravity to 1
            _rigidbody.gravityScale = 1;
        }
    }



    /* ################################################################################################################## */



    private void updatePlayerState()
    {
        //check if character is grounded using the checkGrounded() function and updates the _isGrounded variable
        _isGrounded = checkGrounded();
        //set IsGrounded parameter in animator to the value assigned above
        _animator.SetBool("IsGround", _isGrounded);

        //get character's vertical velocity from rigidbody component
        float verticalVelocity = _rigidbody.velocity.y;
        //set IsDown in animator to true if the condition of verticalVelocity is < 0
        _animator.SetBool("IsDown", verticalVelocity < 0);

        //check if player is grounded and not falling to make sure everything is reset after falling
        if (_isGrounded && verticalVelocity == 0)
        {
            _animator.SetBool("IsJump", false);
            _animator.ResetTrigger("IsJumpFirst");
            _animator.ResetTrigger("IsJumpSecond");
            _animator.SetBool("IsDown", false);

            jumpLeft = 2;
            _isClimb = false;
            _isSprintable = true;
        }
        else if (_isClimb) //this set jumpLeft to 1 when climbing because when player walk off cliff can double jump
        {
            // one remaining jump chance after climbing
            jumpLeft = 1;
        }
    }

    private void move()
    {
        // calculate movement
        float horizontalMovement = Input.GetAxis("Horizontal") * moveSpeed;

        // set velocity
        Vector2 newVelocity;
        newVelocity.x = horizontalMovement;
        newVelocity.y = _rigidbody.velocity.y;
        _rigidbody.velocity = newVelocity;

        //only change sprite facing direction when character is not climbing
        if (!_isClimb)
        {
            // the sprite itself is inversed 
            float moveDirection = -transform.localScale.x * horizontalMovement;

            if (moveDirection < 0)
            {
                // flip player sprite
                Vector3 newScale;
                newScale.x = horizontalMovement < 0 ? 1 : -1;
                newScale.y = 1;
                newScale.z = 1;

                transform.localScale = newScale;

                //only trigger rotate animation if character is grounded, in air no have rotate animation
                if (_isGrounded)
                {
                    // turn back animation
                    _animator.SetTrigger("IsRotate");
                }
            }
            else if (moveDirection > 0)
            {
                // move forward
                _animator.SetBool("IsRun", true);
            }
        }

        // stop
        // if no horizontal input, trigger "stopTrigger" animation, reset isRotate trigger, set IsRun to false
        if (Input.GetAxis("Horizontal") == 0)
        {
            _animator.SetTrigger("stopTrigger");
            _animator.ResetTrigger("IsRotate");
            _animator.SetBool("IsRun", false);
        }
        else // if got horizontal input, reset stopTrigger to not yet trigger
        {
            _animator.ResetTrigger("stopTrigger");
        }
    }

    private void jumpControl()
    {
        // GetButtonDown means get input when player PRESSED DOWN the key, so if no then do nothing
        if (!Input.GetButtonDown("Jump"))
            return;

        //if is climbing, trigger climbJump() instead of normal jumping
        if (_isClimb)
            climbJump();
        else if (jumpLeft > 0) //checking whether there is remaining number of jumps left
            jump();
    }

    private void fallControl()
    {
        // GetButtonUp means get input when player RELEASE the key
        // So if got pressed key and then RELEASE && character is not climbing, set _isFalling to true and execute fall() function
        if (Input.GetButtonUp("Jump") && !_isClimb)
        {
            _isFalling = true;
            fall();
        }
        else
        {
            _isFalling = false;
        }
    }

    private void sprintControl()
    {
        // check if PRESSED DOWN the key 'K' && can sprint && sprint is resetted
        if (Input.GetKeyDown(KeyCode.K) && _isSprintable && _isSprintReset)
            sprint();
    }

    private void attackControl()
    {
        //check if PRESSED DOWN the key 'J' && is not climbing && can attack
        if (Input.GetKeyDown(KeyCode.J) && !_isClimb && _isAttackable)
            attack();
    }



    /* ################################################################################################################## */



    private void die()
    {
        // trigger IsDead trigger in animator
        _animator.SetTrigger("IsDead");

        // set character unable to input keys
        _isInputEnabled = false;

        // stop player movement
        Vector2 newVelocity;
        newVelocity.x = 0;
        newVelocity.y = 0;
        _rigidbody.velocity = newVelocity;

        // visual effect
        _spriteRenderer.color = invulnerableColor;

        // death recoil
        Vector2 newForce;
        newForce.x = -_transform.localScale.x * deathRecoil.x;
        newForce.y = deathRecoil.y;
        _rigidbody.AddForce(newForce, ForceMode2D.Impulse);

        StartCoroutine(deathCoroutine());
    }

    private IEnumerator deathCoroutine()
    {
        //retrieves the shared material of the _boxCollider, which allow to modify its properties like bounciness and friction
        var material = _boxCollider.sharedMaterial;
        material.bounciness = 0.3f;
        material.friction = 0.3f;
        // unity bug, need to disable and then enable it to apply the changes to its material properties
        _boxCollider.enabled = false;
        _boxCollider.enabled = true;

        //pauses corountine for duration of deathDelay, allowing time for any death animations or effects to play finish
        yield return new WaitForSeconds(deathDelay);

        //remove bounce and friction effects
        material.bounciness = 0;
        material.friction = 0;
        //reloads the current scene, effectively restarting the game
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }



    /* ######################################################### */



    private bool checkGrounded()
    {
        //below these lines are assigning variables for CircleCast, so there is 5 things to assign
        Vector2 origin = _transform.position;

        float radius = 0.2f;

        // detect downwards
        Vector2 direction;
        direction.x = 0;
        direction.y = -1;

        float distance = 0.5f;
        LayerMask layerMask = LayerMask.GetMask("Platform");

        //use CircleCast which is a type of raycast to detect collision, follow format use jiu dui le
        RaycastHit2D hitRec = Physics2D.CircleCast(origin, radius, direction, distance, layerMask);
        //return true if circlecast hit a collider and false if hit nothing
        return hitRec.collider != null;
    }

    private void jump()
    {
        //set new velocity for jumping, x equals current horizontal velocity and y equals jumpSpeed
        Vector2 newVelocity;
        newVelocity.x = _rigidbody.velocity.x;
        newVelocity.y = jumpSpeed;

        _rigidbody.velocity = newVelocity;

        //set IsJump in animator to true
        _animator.SetBool("IsJump", true);
        //set numbers of jump left to minus 1
        jumpLeft -= 1;
        //figuring which jump to trigger to true in animator
        if (jumpLeft == 0)
        {
            _animator.SetTrigger("IsJumpSecond");
        }
        else if (jumpLeft == 1)
        {
            _animator.SetTrigger("IsJumpFirst");
        }
    }

    private void climbJump()
    {
        Vector2 realClimbJumpForce;
        realClimbJumpForce.x = climbJumpForce.x * transform.localScale.x;
        realClimbJumpForce.y = climbJumpForce.y;
        _rigidbody.AddForce(realClimbJumpForce, ForceMode2D.Impulse);

        //set IsClimbJump and IsJumpFirst to true in animator
        _animator.SetTrigger("IsClimbJump");
        _animator.SetTrigger("IsJumpFirst");

        //set character cant move after climbjump
        _isInputEnabled = false;
        //start coroutine of climbJumpCoroutine(_climbJumpDelay)
        StartCoroutine(climbJumpCoroutine(_climbJumpDelay));
    }

    private IEnumerator climbJumpCoroutine(float delay)
    {
        //pauses coroutine for a duration of delay before enabling character to move again
        yield return new WaitForSeconds(delay);
        //enable character to move
        _isInputEnabled = true;
        //reset trigger of IsClimbJump in animator
        _animator.ResetTrigger("IsClimbJump");

        // jump to the opposite direction
        //which just means set the direction of the character to face at the direction of jumping
        Vector3 newScale;
        newScale.x = -transform.localScale.x;
        newScale.y = 1;
        newScale.z = 1;

        transform.localScale = newScale;
    }

    private void fall()
    {
        Vector2 newVelocity;
        newVelocity.x = _rigidbody.velocity.x;
        newVelocity.y = -fallSpeed;

        _rigidbody.velocity = newVelocity;
    }

    private void sprint()
    {
        // reject input during sprinting
        _isInputEnabled = false;
        _isSprintable = false;
        _isSprintReset = false;

        //set new velocity for sprinting
        //in this case the character scale when facing right is negative, so when character not climbing, sprintSpeed need negative
        Vector2 newVelocity;
        newVelocity.x = transform.localScale.x * (_isClimb ? sprintSpeed : -sprintSpeed);
        newVelocity.y = 0;

        _rigidbody.velocity = newVelocity;

        //Check if is climbing
        if (_isClimb)
        {
            // sprint to the opposite direction
            //which is just mean set the direction of the character to face at the direction of sprinting when climbing
            Vector3 newScale;
            newScale.x = -transform.localScale.x;
            newScale.y = 1;
            newScale.z = 1;

            transform.localScale = newScale;
        }

        //set IsSprint to true in animator
        _animator.SetTrigger("IsSprint");
        //start coroutine of sprintCoroutine() 
        StartCoroutine(sprintCoroutine(sprintTime, sprintInterval));
    }

    private IEnumerator sprintCoroutine(float sprintDelay, float sprintInterval)
    {
        //pauses coroutine for a duration of sprintDelay before enabling character input
        //so that the character cant move immediately after sprint starts
        yield return new WaitForSeconds(sprintDelay);
        //enable character input and re-enable character to be able to sprint again
        _isInputEnabled = true;
        _isSprintable = true;

        //pauses coroutine for a duration of sprintInterval before resetting sprintReset to true
        //so that the character cant immediately sprint right after sprint ends
        yield return new WaitForSeconds(sprintInterval);
        _isSprintReset = true;
    }

    private void attack()
    {
        //assign a new variable to check whether player got press up key or down key and store them
        float verticalDirection = Input.GetAxis("Vertical");
        //run attackUp() if verticalDirection got up inputs which is >0
        if (verticalDirection > 0)
            attackUp();
        else if (verticalDirection < 0 && !_isGrounded) //run attackDown if verticalDirection got down inputs which is <0 AND character is not grounded
            attackDown();
        else
            attackForward();
    }

    private void attackUp()
    {
        //set isAttackUp in animator to true
        _animator.SetTrigger("IsAttackUp");
        //activates the attackUpEffect GameObject which is the sword slicing effect under the parent object in unity
        attackUpEffect.SetActive(true);

        Vector2 detectDirection;
        detectDirection.x = 0;
        detectDirection.y = 1;

        StartCoroutine(attackCoroutine(attackUpEffect, _attackEffectLifeTime, attackInterval, detectDirection, attackUpRecoil));
    }

    private void attackForward()
    {
        _animator.SetTrigger("IsAttack");
        attackForwardEffect.SetActive(true);

        Vector2 detectDirection;
        detectDirection.x = -transform.localScale.x;
        detectDirection.y = 0;

        Vector2 recoil;
        recoil.x = transform.localScale.x > 0 ? -attackForwardRecoil.x : attackForwardRecoil.x;
        recoil.y = attackForwardRecoil.y;

        StartCoroutine(attackCoroutine(attackForwardEffect, _attackEffectLifeTime, attackInterval, detectDirection, recoil));
    }

    private void attackDown()
    {
        _animator.SetTrigger("IsAttackDown");
        attackDownEffect.SetActive(true);

        Vector2 detectDirection;
        detectDirection.x = 0;
        detectDirection.y = -1;

        StartCoroutine(attackCoroutine(attackDownEffect, _attackEffectLifeTime, attackInterval, detectDirection, attackDownRecoil));
    }

    private IEnumerator attackCoroutine(GameObject attackEffect, float effectDelay, float attackInterval, Vector2 detectDirection, Vector2 attackRecoil)
    {
        //below lines are assigning variables for CircleCastAll
        Vector2 origin = _transform.position;

        float radius = 0.6f;

        float distance = 1.5f;
        LayerMask layerMask = LayerMask.GetMask("Enemy") | LayerMask.GetMask("Trap") | LayerMask.GetMask("Switch") | LayerMask.GetMask("Projectile");

        RaycastHit2D[] hitRecList = Physics2D.CircleCastAll(origin, radius, detectDirection, distance, layerMask);

        foreach (RaycastHit2D hitRec in hitRecList)
        {
            GameObject obj = hitRec.collider.gameObject;

            string layerName = LayerMask.LayerToName(obj.layer);
            /*
            if (layerName == "Switch")
            {
                Switch swithComponent = obj.GetComponent<Switch>();
                if (swithComponent != null)
                    swithComponent.turnOn();
            }
            else if (layerName == "Enemy")
            {
                EnemyController enemyController = obj.GetComponent<EnemyController>();
                if (enemyController != null)
                    enemyController.hurt(1);
            }
            else if (layerName == "Projectile")
            {
                Destroy(obj);
            }
            */
        }
        
        if (hitRecList.Length > 0)
        {
            _rigidbody.velocity = attackRecoil;
        }

        yield return new WaitForSeconds(effectDelay);

        attackEffect.SetActive(false);

        // attack cool down
        _isAttackable = false;
        yield return new WaitForSeconds(attackInterval);
        _isAttackable = true;
    }
}
