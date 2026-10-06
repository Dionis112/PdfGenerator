<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:fo="http://www.w3.org/1999/XSL/Format"
  xmlns:b="urn:pdfgenerator:bank-test" xmlns:a="http://www.altova.com/xslt-extensions" exclude-result-prefixes="xs b a">
  <xsl:import href="bank-standard.xsl"/>
  <!-- Combined probe: the current application must report missing extensions, never claim parity. -->
  <xsl:function name="b:due-date" as="xs:date"><xsl:param name="date" as="xs:date"/><xsl:param name="month" as="xs:integer"/><xsl:sequence select="a:add-months-to-date($date,$month)"/></xsl:function>
  <xsl:function name="b:service-total" as="xs:decimal"><xsl:param name="document" as="element(bank-document)"/><xsl:sequence select="a:evaluate('sum($p1/service[@selected = &quot;true&quot;]/@amount)', $document/services)"/></xsl:function>
  <xsl:function name="b:row-number" as="xs:integer"><xsl:param name="index" as="xs:integer"/><xsl:sequence select="a:generate-auto-number('bank-payment',1,1,'schedule')"/></xsl:function>
  <xsl:template name="b:reset-numbers"><fo:block font-size="7pt">Сброс нумерации: <xsl:value-of select="a:reset-auto-number('bank-payment')"/></fo:block></xsl:template>
  <xsl:template name="b:clauses"><xsl:for-each select="a:distinct-nodes(clauses/clause)"><fo:block space-after="3mm"><xsl:value-of select="concat(position(),'. ',.)"/></fo:block></xsl:for-each></xsl:template>
</xsl:stylesheet>
