import React from "react";
import Translate from "@docusaurus/Translate";
import CodeBlock from "@theme/CodeBlock";
import { FaKeyboard, FaRobot, FaRunning } from "react-icons/fa";
import styles from "./styles.module.css";

const points = [
  {
    icon: FaKeyboard,
    title: <Translate id="homepage.surface.device.title">A keyboard writes it</Translate>,
    text: (
      <Translate id="homepage.surface.device.text">
        A device writer stamps each press with the current tick, from Update or FixedUpdate.
      </Translate>
    ),
  },
  {
    icon: FaRobot,
    title: <Translate id="homepage.surface.agent.title">So does a script</Translate>,
    text: (
      <Translate id="homepage.surface.agent.text">
        A scripted agent fills the same fields. Nothing distinguishes it from a player.
      </Translate>
    ),
  },
  {
    icon: FaRunning,
    title: <Translate id="homepage.surface.reader.title">The reader can't tell</Translate>,
    text: (
      <Translate id="homepage.surface.reader.text">
        It reads only the surface, so every mechanic written for a player works for an agent the day it ships.
      </Translate>
    ),
  },
];

export default function Surface() {
  return (
    <section>
      <div className="container">
        <h2 className="text--center">
          <Translate id="homepage.surface.title">One surface, any writer</Translate>
        </h2>
        <p className={`text--center ${styles.lead}`}>
          <Translate id="homepage.surface.lead">
            Put a project's input vocabulary in a plain type, and have every reader read only that.
          </Translate>
        </p>
        <div className={styles.layout}>
          <div className={styles.code}>
            <CodeBlock language="cs">
              {`public class CharacterInput : MonoBehaviour
{
    public Vector2 Move;                                  // continuous
    [FixedInputWindow(5)] public FixedInputEvent Attack;  // discrete, lenient
    public FixedInputEvent Evade;                         // discrete, frame-exact
}`}
            </CodeBlock>
          </div>
          <ul className={styles.points}>
            {points.map((point, idx) => (
              <li key={idx} className={styles.point}>
                <point.icon size={20} className={styles.pointIcon} aria-hidden="true" />
                <div>
                  <h3 className={styles.pointTitle}>{point.title}</h3>
                  <p className={styles.pointText}>{point.text}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}
