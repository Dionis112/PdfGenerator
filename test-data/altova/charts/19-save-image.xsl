<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
 xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
 xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:t="urn:altova-fixture:taxonomy"
 exclude-result-prefixes="xs a xbrli t">
 <xsl:output method="xml" encoding="utf-8" indent="yes"/>
 <!-- Pass an existing, fresh absolute filesystem directory, not a file URI. -->
 <xsl:param name="output-directory" as="xs:string" required="yes"/>
 <xsl:template match="/chart">
  <xsl:if test="not(normalize-space($output-directory))">
   <xsl:message terminate="yes">output-directory must not be empty.</xsl:message>
  </xsl:if>
  <xsl:variable name="config" select="a:create-chart-config('BarChart', string(@title))"/>
  <xsl:variable name="series" select="a:create-chart-data-series('Тест', point/@label/string(), point/@value/xs:double(.))"/>
  <xsl:variable name="chart" select="a:create-chart($config, $series)"/>
  <xsl:variable name="default-file" select="concat($output-directory, '/altova-19-default.img')"/>
  <xsl:variable name="png-file" select="concat($output-directory, '/altova-19-explicit.png')"/>
  <result>
   <saved file="{$default-file}" format="default"><xsl:sequence select="a:save-chart-image($chart, $default-file, string(@width), string(@height))"/></saved>
   <saved file="{$png-file}" format="png"><xsl:sequence select="a:save-chart-image($chart, $png-file, string(@width), string(@height), 'png')"/></saved>
  </result>
 </xsl:template>
</xsl:stylesheet>
