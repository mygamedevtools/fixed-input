import React from 'react';
import Translate from '@docusaurus/Translate';
import CodeBlock from '@theme/CodeBlock';
import styles from './styles.module.css';

export default function QuickStart() {
  return (
    <section>
      <div className="container">
        <h2 className="text--center"><Translate id="homepage.example.title">An intent instead of a flag</Translate></h2>
        <p className={styles.caption}><Translate id="homepage.example.text1">Write and read input like this:</Translate></p>
        <CodeBlock language="cs">
          {`// A writer, wherever input comes from.
input.Attack.Set(MyFixedTick.Current);

// A reader, later in the same step.
if (input.Attack.TryConsume(tick))
    Attack();`}
        </CodeBlock>
        <p className={styles.caption}><Translate id="homepage.example.text2">Instead of:</Translate></p>
        <CodeBlock language="cs">
          {`bool _attackPressed;

void Update()
{
    if (Keyboard.current.jKey.wasPressedThisFrame)
        _attackPressed = true;
}

void FixedUpdate()
{
    if (_attackPressed)
    {
        _attackPressed = false;
        Attack();
    }
}`}
        </CodeBlock>
        <p className={styles.caption}>
          <Translate id="homepage.example.text3">
            The flag stays set until something clears it, so a paused game delivers a stale press and a second reader never sees it. An intent is valid for exactly one fixed step and expires by arithmetic.
          </Translate>
        </p>
      </div>
    </section>
  );
}
