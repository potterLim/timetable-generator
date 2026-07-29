---
layout: default
title: Timetable Generator
description: 수강 조건과 개인 일정을 반영한 시간표를 비교하고, 이미지 또는 캘린더로 내보낼 수 있는 데스크톱 앱입니다.
permalink: /
---

<section class="hero">
  <h1>Timetable Generator</h1>
  <p class="hero__tagline">수강 조건에 맞는 시간표를 한눈에 비교하세요.</p>
  <p class="hero__summary">
    과목과 분반의 우선순위를 정하면 개인 일정과 겹치지 않는 시간표를 자동으로 구성합니다.<br>
    완성한 시간표는 이미지로 저장하거나 캘린더로 내보낼 수 있습니다.
  </p>
  <div class="actions">
    <a class="button button--primary" href="https://github.com/potterLim/timetable-generator/releases">최신 버전 다운로드</a>
    <a class="button" href="{{ '/guide/' | relative_url }}">사용 방법 보기</a>
  </div>
</section>

<section class="product-section" aria-labelledby="start-title">
  <div class="section-heading">
    <p class="eyebrow">WORKFLOW</p>
    <h2 id="start-title">과목 선택부터 내보내기까지</h2>
  </div>
  <ol class="step-grid">
    <li class="step-card">
      <span class="step-card__number" aria-hidden="true">1</span>
      <h3>과목과 분반을 선택하세요</h3>
      <p>과목을 검색하고 분반별 우선순위를 정합니다.</p>
    </li>
    <li class="step-card">
      <span class="step-card__number" aria-hidden="true">2</span>
      <h3>가능한 시간표를 비교하세요</h3>
      <p>수강 조건과 개인 일정이 반영된 결과를 한눈에 확인합니다.</p>
    </li>
    <li class="step-card">
      <span class="step-card__number" aria-hidden="true">3</span>
      <h3>시간표를 저장하거나 내보내세요</h3>
      <p>PNG 이미지로 저장하거나 사용하는 캘린더에 추가합니다.</p>
    </li>
  </ol>
</section>

<section class="product-section" aria-labelledby="export-title">
  <div class="section-heading">
    <p class="eyebrow">EXPORT</p>
    <h2 id="export-title">시간표를 원하는 방식으로 내보내세요</h2>
  </div>
  <div class="export-grid">
    <article class="export-card">
      <h3>PNG 이미지로 저장</h3>
      <p>
        현재 시간표 한 장이나 모든 가능한 시간표를 PNG로 저장할 수 있습니다.
        각 이미지의 요일과 시간 범위는 일정에 맞춰 자동으로 조정됩니다.
      </p>
    </article>
    <article class="export-card">
      <h3>캘린더로 내보내기</h3>
      <p>
        Google Calendar는 Windows와 macOS에서, Apple Calendar는 macOS에서 사용할 수 있습니다.
        수업과 개인 일정은 학기 기간에 맞는 반복 일정으로 추가됩니다.
      </p>
    </article>
  </div>
  <p class="section-note">
    내보내기를 시작하면 필요한 경우 캘린더 연결과 접근 권한을 요청합니다.
  </p>
</section>

<section class="product-section support-panel" aria-labelledby="support-title">
  <div class="support-panel__copy">
    <p class="eyebrow">SUPPORT</p>
    <h2 id="support-title">사용 방법과 지원</h2>
    <p>사용 방법을 확인하거나 해결되지 않은 문제를 알려 주세요.</p>
  </div>
  <div class="support-panel__actions">
    <a class="support-link" href="{{ '/guide/' | relative_url }}">
      <span>
        <strong>사용 방법</strong>
        <small>처음 시작부터 내보내기까지 확인하기</small>
      </span>
      <span class="support-link__arrow" aria-hidden="true">→</span>
    </a>
    <a class="support-link" href="https://github.com/potterLim/timetable-generator/issues">
      <span>
        <strong>문제 제보</strong>
        <small>오류나 사용 문제를 GitHub Issues에 남기기</small>
      </span>
      <span class="support-link__arrow" aria-hidden="true">→</span>
    </a>
  </div>
</section>
