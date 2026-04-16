from __future__ import annotations

from pathlib import Path

import altair as alt
import pandas as pd


ROOT_DIR = Path(__file__).resolve().parent.parent
RAW_RESULTS_PATH = ROOT_DIR / "benchmarks" / "results" / "gdelib-1.4.0-benchmark-raw.csv"
ASSETS_DIR = ROOT_DIR / "docs" / "assets" / "benchmarks"
RESULTS_DIR = ROOT_DIR / "benchmarks" / "results"

SCENARIO_ORDER = ["Scalar_400", "Scalar_4000", "Files_3x64KB", "Mixed_4003"]
SCENARIO_LABELS = {
    "Scalar_400": "Scalar_400",
    "Scalar_4000": "Scalar_4000",
    "Files_3x64KB": "Files_3x64KB",
    "Mixed_4003": "Mixed_4003",
}
MODE_LABELS = {
    "TwoFile": "Двухфайловый",
    "OneFile": "Однофайловый",
}
MODE_ORDER = ["Двухфайловый", "Однофайловый"]
MODE_COLORS = ["#1f77b4", "#e67e22"]


def main() -> None:
    raw_df = pd.read_csv(RAW_RESULTS_PATH, encoding="utf-8-sig")
    summary_df = build_summary(raw_df)

    ASSETS_DIR.mkdir(parents=True, exist_ok=True)
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)

    time_base_chart = build_time_chart(summary_df)
    size_base_chart = build_size_chart(summary_df)
    artifacts_base_chart = build_artifacts_chart(summary_df)

    time_chart = apply_common_style(time_base_chart)
    size_chart = apply_common_style(size_base_chart)
    artifacts_chart = apply_common_style(artifacts_base_chart)
    dashboard = apply_common_style(
        alt.vconcat(time_base_chart, size_base_chart, artifacts_base_chart).resolve_scale(color="independent")
    )

    time_chart.save(ASSETS_DIR / "benchmark-time.svg")
    size_chart.save(ASSETS_DIR / "benchmark-size.svg")
    artifacts_chart.save(ASSETS_DIR / "benchmark-artifacts.svg")
    dashboard.save(RESULTS_DIR / "gdelib-1.4.0-benchmark-dashboard.html")


def build_summary(raw_df: pd.DataFrame) -> pd.DataFrame:
    summary_df = (
        raw_df.groupby(["Scenario", "Description", "Mode"], as_index=False)
        .agg(
            SaveMsMean=("SaveMs", "mean"),
            SaveMsStd=("SaveMs", "std"),
            OpenAllMsMean=("OpenAllMs", "mean"),
            OpenAllMsStd=("OpenAllMs", "std"),
            ContainerBytes=("ContainerBytes", "mean"),
            ArtifactCount=("ArtifactCount", "mean"),
        )
        .fillna(0.0)
    )

    summary_df["ScenarioLabel"] = summary_df["Scenario"].map(SCENARIO_LABELS)
    summary_df["ModeLabel"] = summary_df["Mode"].map(MODE_LABELS)
    summary_df["ContainerKiB"] = summary_df["ContainerBytes"] / 1024
    return summary_df


def build_time_chart(summary_df: pd.DataFrame) -> alt.Chart:
    time_df = pd.concat(
        [
            prepare_metric_frame(summary_df, "SaveMsMean", "SaveMsStd", "Save()"),
            prepare_metric_frame(summary_df, "OpenAllMsMean", "OpenAllMsStd", "OpenAll()"),
        ],
        ignore_index=True,
    )

    base = alt.Chart(time_df).encode(
        x=alt.X(
            "ScenarioLabel:N",
            title="Сценарий",
            sort=SCENARIO_ORDER,
            axis=alt.Axis(labelAngle=0, labelLimit=140),
        ),
        xOffset=alt.XOffset("ModeLabel:N", sort=MODE_ORDER),
        color=alt.Color(
            "ModeLabel:N",
            title="Режим",
            sort=MODE_ORDER,
            scale=alt.Scale(domain=MODE_ORDER, range=MODE_COLORS),
        ),
    )

    bars = base.mark_bar(size=26).encode(
        y=alt.Y("MeanMs:Q", title="Время, мс"),
        tooltip=[
            alt.Tooltip("ScenarioLabel:N", title="Сценарий"),
            alt.Tooltip("ModeLabel:N", title="Режим"),
            alt.Tooltip("MetricLabel:N", title="Операция"),
            alt.Tooltip("MeanMs:Q", title="Среднее, мс", format=".3f"),
            alt.Tooltip("StdMs:Q", title="Ст. откл., мс", format=".3f"),
        ],
    )

    errors = base.mark_errorbar(color="#555555").encode(
        y=alt.Y("LowerMs:Q", title=None),
        y2=alt.Y2("UpperMs:Q"),
    )

    chart = alt.layer(bars, errors, data=time_df).facet(
        column=alt.Column(
            "MetricLabel:N",
            title=None,
            sort=["Save()", "OpenAll()"],
            header=alt.Header(labelFontSize=13, labelPadding=10),
        )
    )

    return chart.properties(
        title="Сравнение среднего времени операций Save() и OpenAll()",
        bounds="flush",
    )


def build_size_chart(summary_df: pd.DataFrame) -> alt.Chart:
    chart = (
        alt.Chart(summary_df)
        .mark_bar(size=26)
        .encode(
            x=alt.X(
                "ScenarioLabel:N",
                title="Сценарий",
                sort=SCENARIO_ORDER,
                axis=alt.Axis(labelAngle=0, labelLimit=140),
            ),
            xOffset=alt.XOffset("ModeLabel:N", sort=MODE_ORDER),
            y=alt.Y("ContainerKiB:Q", title="Размер контейнера, КиБ"),
            color=alt.Color(
                "ModeLabel:N",
                title="Режим",
                sort=MODE_ORDER,
                scale=alt.Scale(domain=MODE_ORDER, range=MODE_COLORS),
            ),
            tooltip=[
                alt.Tooltip("ScenarioLabel:N", title="Сценарий"),
                alt.Tooltip("ModeLabel:N", title="Режим"),
                alt.Tooltip("ContainerBytes:Q", title="Размер, байт", format=".0f"),
                alt.Tooltip("ContainerKiB:Q", title="Размер, КиБ", format=".2f"),
            ],
        )
        .properties(
            title="Сравнение итогового размера контейнера",
            width=640,
            height=300,
        )
    )

    return chart


def build_artifacts_chart(summary_df: pd.DataFrame) -> alt.Chart:
    chart = (
        alt.Chart(summary_df)
        .mark_bar(size=26)
        .encode(
            x=alt.X(
                "ScenarioLabel:N",
                title="Сценарий",
                sort=SCENARIO_ORDER,
                axis=alt.Axis(labelAngle=0, labelLimit=140),
            ),
            xOffset=alt.XOffset("ModeLabel:N", sort=MODE_ORDER),
            y=alt.Y("ArtifactCount:Q", title="Число артефактов"),
            color=alt.Color(
                "ModeLabel:N",
                title="Режим",
                sort=MODE_ORDER,
                scale=alt.Scale(domain=MODE_ORDER, range=MODE_COLORS),
            ),
            tooltip=[
                alt.Tooltip("ScenarioLabel:N", title="Сценарий"),
                alt.Tooltip("ModeLabel:N", title="Режим"),
                alt.Tooltip("ArtifactCount:Q", title="Число артефактов", format=".1f"),
            ],
        )
        .properties(
            title="Сравнение числа файловых артефактов после сохранения",
            width=640,
            height=300,
        )
    )

    return chart


def prepare_metric_frame(
    summary_df: pd.DataFrame,
    mean_column: str,
    std_column: str,
    metric_label: str,
) -> pd.DataFrame:
    metric_df = summary_df[
        ["Scenario", "ScenarioLabel", "Description", "Mode", "ModeLabel", mean_column, std_column]
    ].copy()
    metric_df["MetricLabel"] = metric_label
    metric_df["MeanMs"] = metric_df[mean_column]
    metric_df["StdMs"] = metric_df[std_column]
    metric_df["LowerMs"] = (metric_df["MeanMs"] - metric_df["StdMs"]).clip(lower=0)
    metric_df["UpperMs"] = metric_df["MeanMs"] + metric_df["StdMs"]
    return metric_df


def apply_common_style(chart: alt.Chart) -> alt.Chart:
    return (
        chart.configure(
            background="white",
            padding={"left": 16, "top": 14, "right": 16, "bottom": 12},
        )
        .configure_title(anchor="start", font="DejaVu Sans", fontSize=16, color="#1f1f1f")
        .configure_axis(
            labelFont="DejaVu Sans",
            titleFont="DejaVu Sans",
            labelFontSize=11,
            titleFontSize=12,
            gridColor="#d9d9d9",
            domainColor="#7f7f7f",
            tickColor="#7f7f7f",
        )
        .configure_header(
            labelFont="DejaVu Sans",
            titleFont="DejaVu Sans",
            labelColor="#1f1f1f",
            titleColor="#1f1f1f",
        )
        .configure_legend(
            labelFont="DejaVu Sans",
            titleFont="DejaVu Sans",
            labelFontSize=11,
            titleFontSize=12,
            orient="top",
            direction="horizontal",
            offset=8,
        )
        .configure_view(stroke=None)
    )


if __name__ == "__main__":
    main()
