import React from "react";
import clsx from "clsx";
import Translate from '@docusaurus/Translate';
import { FaHourglassEnd, FaCog, FaExchangeAlt, FaLayerGroup, FaSearch, FaCubes } from "react-icons/fa";
import styles from './styles.module.css';

const features = [
  {
    title: <Translate id="homepage.feature.expiry.title">Expires by arithmetic</Translate>,
    description: <Translate id="homepage.feature.expiry.text">Nothing clears an intent and nothing consumes it twice. A paused or reloaded game cannot deliver a stale press.</Translate>,
    icon: FaHourglassEnd,
  },
  {
    title: <Translate id="homepage.feature.tick.title">A tick that installs itself</Translate>,
    description: <Translate id="homepage.feature.tick.text">The default clock joins the end of the fixed phase. No scene object, no prefab, no script execution order.</Translate>,
    icon: FaCog,
  },
  {
    title: <Translate id="homepage.feature.buffering.title">Buffering per read</Translate>,
    description: <Translate id="homepage.feature.buffering.text">The window is an argument to each read, so one press can be frame-exact for one reader and lenient for another.</Translate>,
    icon: FaLayerGroup,
  },
  {
    title: <Translate id="homepage.feature.clocks.title">Bring your own clock</Translate>,
    description: <Translate id="homepage.feature.clocks.text">ITickSource is an interface. Run off a netcode tick, a lockstep frame or a manual clock in tests.</Translate>,
    icon: FaExchangeAlt,
  },
  {
    title: <Translate id="homepage.feature.diagnostics.title">Diagnostics that name it</Translate>,
    description: <Translate id="homepage.feature.diagnostics.text">The editor reports an intent nobody consumed and two writers that fought over a field, by name and call site.</Translate>,
    icon: FaSearch,
  },
  {
    title: <Translate id="homepage.feature.entities.title">Entities, if you want it</Translate>,
    description: <Translate id="homepage.feature.entities.text">Two unmanaged fields, so an intent drops into an IComponentData. The ECS clock compiles only when Entities is installed.</Translate>,
    icon: FaCubes,
  },
];

export default function Features() {
  return (
    <section>
      <div className="container">
        <div className="row">
          {features.map((feature, idx) => (
            <div key={idx} className={clsx("col col--4 margin-vert--md")}>
              <feature.icon size={34} className={styles.featureIcon} />
              <h3 className={styles.featureTitle}>{feature.title}</h3>
              <p className={styles.featureText}>{feature.description}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
