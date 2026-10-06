<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
 xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
 xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:t="urn:altova-fixture:taxonomy"
 exclude-result-prefixes="xs a xbrli t">
 <xsl:output method="xml" encoding="utf-8" indent="yes"/>
 <xsl:template match="/chart">
  <xsl:variable name="config" select="a:create-chart-config('BarChart', string(@title))"/>
  <xsl:variable name="overlay-config" select="a:create-chart-config('LineChart', 'Факт')"/>
  <xsl:variable name="plan" select="a:create-chart-data-series('План', plan/point/@label/string(), plan/point/@value/xs:double(.))"/>
  <xsl:variable name="actual" select="a:create-chart-data-series('Факт', actual/point/@label/string(), actual/point/@value/xs:double(.))"/>
  <xsl:variable name="layer" select="a:create-chart-layer($overlay-config, $actual)"/>
  <result>
   <image merge="default" encoding="base64" format="png"><xsl:value-of select="a:generate-chart-image(a:create-multi-layer-chart($config, $plan, $layer), string(@width), string(@height), 'base64Binary', 'png')"/></image>
   <image merge="true" encoding="base64" format="png"><xsl:value-of select="a:generate-chart-image(a:create-multi-layer-chart($config, $plan, $layer, true()), string(@width), string(@height), 'base64Binary', 'png')"/></image>
   <image merge="false" encoding="base64" format="png"><xsl:value-of select="a:generate-chart-image(a:create-multi-layer-chart($config, $plan, $layer, false()), string(@width), string(@height), 'base64Binary', 'png')"/></image>
  </result>
 </xsl:template>
</xsl:stylesheet>
