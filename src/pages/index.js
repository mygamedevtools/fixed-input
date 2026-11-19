import React from "react";
import Layout from "@theme/Layout";

import Hero from "@site/src/components/HomepageHero"
import Proof from "@site/src/components/HomepageProof"
import QuickStart from "@site/src/components/HomepageQuickStart"
import Surface from "@site/src/components/HomepageSurface"
import Features from "@site/src/components/HomepageFeatures"
import Installation from "@site/src/components/HomepageInstallation"

export default function Home() {
  return (
    <Layout description="A deterministic fixed-step tick and a one-shot input intent for Unity. One input surface, written by a keyboard or a script, read the same way by both.">
      <Hero />
      <main>
        {/* The argument runs claim -> proof -> detail -> ask. The code comparison is the
            most convincing thing on the page, so it goes first; Installation goes last,
            because asking for the install while an argument is still pending loses both. */}
        <Proof />
        <QuickStart />
        <Surface />
        <Features />
        <Installation />
      </main>
    </Layout>
  );
}
