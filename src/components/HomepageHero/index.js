import React, { useEffect, useRef } from "react";
import Link from "@docusaurus/Link";
import clsx from "clsx";
import styles from "./styles.module.css";
import Translate from '@docusaurus/Translate';

/** Steps of history each lane shows, oldest on the left. */
const CELLS = 32;

/** The script loops over this many steps. */
const CYCLE = 48;

/** Milliseconds per fixed step on screen; slowed down from 20 ms so the eye can follow it. */
const STEP_MS = 190;

const IDLE = 0;
const SET = 1;
const WAITING = 2;
const CONSUMED = 3;
const EXPIRED = 4;

/**
 * Each lane is a list of presses: the step it was set on, and the step a reader consumed it,
 * or null when nobody did and its window lapsed. Same story as the sample's overlay: a buffered
 * attack that waits for the reader, a frame-exact jump, and one press nobody took.
 */
const LANES = [
  {
    name: "Attack",
    window: 5,
    presses: [
      { set: 2, consumed: 6 },
      { set: 18, consumed: null },
      { set: 33, consumed: 34 },
    ],
  },
  {
    name: "Jump",
    window: 0,
    presses: [
      { set: 11, consumed: 11 },
      { set: 26, consumed: 26 },
      { set: 41, consumed: 41 },
    ],
  },
];

/** Precomputes one state per step of the cycle for a lane. */
const buildLane = ({ window, presses }) => {
  const states = new Array(CYCLE).fill(IDLE);
  for (const { set, consumed } of presses) {
    const end = consumed ?? set + window + 1;
    for (let t = set + 1; t < end; t++) states[t % CYCLE] = WAITING;
    states[set % CYCLE] = SET;
    states[end % CYCLE] = consumed === null ? EXPIRED : CONSUMED;
  }
  return states;
};

const LANE_STATES = LANES.map(buildLane);

const CELL_CLASS = {
  [IDLE]: styles.cell,
  [SET]: clsx(styles.cell, styles.cellSet),
  [WAITING]: clsx(styles.cell, styles.cellWaiting),
  [CONSUMED]: clsx(styles.cell, styles.cellConsumed),
  [EXPIRED]: clsx(styles.cell, styles.cellExpired),
};

const mod = (n, m) => ((n % m) + m) % m;

/** The state of a lane's cell `i` when the newest step shown is `tick`. */
const cellState = (lane, tick, i) => LANE_STATES[lane][mod(tick - (CELLS - 1) + i, CYCLE)];

/** Steps since the last press was set, while it is still armed; null otherwise. */
const ageAt = (lane, tick) => {
  const states = LANE_STATES[lane];
  for (let back = 0; back <= LANES[lane].window + 1; back++) {
    const state = states[mod(tick - back, CYCLE)];
    if (state === SET) return back;
    if (state !== WAITING) return null;
  }
  return null;
};

/** Where the counter starts, and the frame anyone with reduced motion keeps. */
const FIRST_TICK = 4096;
const REST = FIRST_TICK + 37;

export default function Hero() {
  const cellRefs = useRef(LANES.map(() => []));
  const ageRefs = useRef([]);
  const tickRef = useRef(null);

  useEffect(() => {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return undefined;

    let tick = REST;
    const render = () => {
      LANES.forEach((_, lane) => {
        cellRefs.current[lane].forEach((el, i) => {
          if (el) el.className = CELL_CLASS[cellState(lane, tick, i)];
        });
        const age = ageAt(lane, tick);
        if (ageRefs.current[lane]) ageRefs.current[lane].textContent = age === null ? "—" : `+${age}`;
      });
      if (tickRef.current) tickRef.current.textContent = String(tick);
    };

    const timer = setInterval(() => {
      tick++;
      render();
    }, STEP_MS);
    return () => clearInterval(timer);
  }, []);

  return (
    // The hero always uses the dark tokens, like the overlay in the sample it mirrors.
    // data-theme scopes the --mgt-* variables to it.
    <header className={clsx("hero", styles.hero)} data-theme="dark">
      <div className={styles.stripes} aria-hidden="true">
        <i />
        <i />
        <i />
      </div>

      <div className={clsx("container", styles.heroContent)}>
        <p className={styles.eyebrow}>
          <span className={styles.eyebrowBars} aria-hidden="true">
            <i />
            <i />
            <i />
          </span>
          <Translate id="homepage.heroEyebrow">Fixed-step input for Unity</Translate>
        </p>
        <h1 className={clsx("hero__title", styles.heroTitle)}>My Fixed Tick</h1>
        <p className={clsx("hero__subtitle", styles.heroSubtitle)}>
          <Translate id="homepage.heroSubtitle">One press, one fixed step.</Translate>
        </p>
        <div className={styles.indexCta}>
          <Link className={clsx("button button--primary button--lg", styles.buttonCta)} to="/docs/intro">
            <Translate id="homepage.callToAction.label">Get Started</Translate>
          </Link>
        </div>

        <div className={styles.rail} aria-hidden="true">
          <div className={styles.railHead}>
            <span className={styles.railTick}>
              TICK <b ref={tickRef}>{REST}</b>
            </span>
            <span className={styles.railOp}>
              <span className={styles.railVar}>input</span>.Attack.
              <span className={styles.railFn}>TryConsume</span>(tick, window:{" "}
              <span className={styles.railNum}>5</span>)
            </span>
          </div>
          {LANES.map((lane, laneIdx) => (
            <div key={lane.name} className={styles.lane}>
              <span className={styles.laneName}>{lane.name}</span>
              <div className={styles.cells}>
                {Array.from({ length: CELLS }, (_, i) => (
                  <i
                    key={i}
                    className={CELL_CLASS[cellState(laneIdx, REST, i)]}
                    ref={(el) => {
                      cellRefs.current[laneIdx][i] = el;
                    }}
                  />
                ))}
              </div>
              <span
                className={styles.laneAge}
                ref={(el) => {
                  ageRefs.current[laneIdx] = el;
                }}
              >
                {(() => {
                  const age = ageAt(laneIdx, REST);
                  return age === null ? "—" : `+${age}`;
                })()}
              </span>
            </div>
          ))}
          <div className={styles.legend}>
            <span><i className={clsx(styles.swatch, styles.cellSet)} />set</span>
            <span><i className={clsx(styles.swatch, styles.cellWaiting)} />buffered</span>
            <span><i className={clsx(styles.swatch, styles.cellConsumed)} />consumed</span>
            <span><i className={clsx(styles.swatch, styles.cellExpired)} />expired</span>
          </div>
        </div>
      </div>
    </header>
  );
}
