<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:fo="http://www.w3.org/1999/XSL/Format"
  xmlns:b="urn:pdfgenerator:bank-test" exclude-result-prefixes="xs b">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:decimal-format name="ru" decimal-separator="," grouping-separator=" "/>
  <xsl:attribute-set name="cell"><xsl:attribute name="padding">2.5mm</xsl:attribute><xsl:attribute name="border-bottom">0.3pt solid #DCE3EA</xsl:attribute></xsl:attribute-set>
  <xsl:attribute-set name="heading"><xsl:attribute name="font-size">14pt</xsl:attribute><xsl:attribute name="color">#173B57</xsl:attribute><xsl:attribute name="space-before">6mm</xsl:attribute><xsl:attribute name="space-after">3mm</xsl:attribute></xsl:attribute-set>
  <xsl:function name="b:money" as="xs:string"><xsl:param name="value" as="xs:decimal"/><xsl:sequence select="format-number($value, '# ##0,00', 'ru')"/></xsl:function>
  <xsl:function name="b:interest" as="xs:decimal">
    <xsl:param name="loan" as="element(loan)"/><xsl:param name="index" as="xs:integer"/>
    <xsl:sequence select="round-half-to-even((xs:decimal($loan/@principal) - ($index - 1) * xs:decimal($loan/@principal) div xs:integer($loan/@months)) * xs:decimal($loan/@annual-rate) div 1200, 2)"/>
  </xsl:function>
  <xsl:function name="b:due-date" as="xs:date"><xsl:param name="date" as="xs:date"/><xsl:param name="month" as="xs:integer"/><xsl:sequence select="$date + xs:yearMonthDuration(concat('P', $month, 'M'))"/></xsl:function>
  <xsl:function name="b:service-total" as="xs:decimal"><xsl:param name="document" as="element(bank-document)"/><xsl:sequence select="sum($document/services/service[@selected='true']/@amount/xs:decimal(.))"/></xsl:function>
  <xsl:function name="b:row-number" as="xs:integer"><xsl:param name="index" as="xs:integer"/><xsl:sequence select="$index"/></xsl:function>
  <xsl:template name="b:reset-numbers"/>
  <xsl:template name="b:clauses"><xsl:for-each-group select="clauses/clause" group-by="normalize-space(.)"><fo:block space-after="3mm"><xsl:value-of select="concat(position(), '. ', current-grouping-key())"/></fo:block></xsl:for-each-group></xsl:template>
  <xsl:template name="header-footer">
    <fo:static-content flow-name="xsl-region-before"><fo:block color="#526777" font-size="8pt" border-bottom="0.5pt solid #DCE3EA" padding-bottom="3mm"><xsl:value-of select="bank/@name"/> / Документы клиента / <xsl:value-of select="@id"/></fo:block></fo:static-content>
    <fo:static-content flow-name="xsl-region-after"><fo:block color="#647585" font-size="8pt" border-top="0.5pt solid #DCE3EA" padding-top="3mm">УЧЕБНЫЙ ОБРАЗЕЦ. СИНТЕТИЧЕСКИЕ ДАННЫЕ.<fo:leader leader-pattern="space"/>Страница <fo:page-number/></fo:block></fo:static-content>
  </xsl:template>
  <xsl:template match="/bank-document">
    <xsl:variable name="doc" select="."/><xsl:variable name="loan" select="loan"/>
    <xsl:variable name="months" select="xs:integer(loan/@months)"/>
    <xsl:variable name="principal" select="xs:decimal(loan/@principal)"/>
    <xsl:variable name="interest" select="sum(for $i in 1 to $months return b:interest($loan,$i))"/>
    <fo:root font-family="DejaVu Sans" font-size="10pt" line-height="1.4" color="#243746">
      <fo:layout-master-set><fo:simple-page-master master-name="bank" page-width="210mm" page-height="297mm" margin="17mm"><fo:region-body margin-top="11mm" margin-bottom="12mm"/><fo:region-before extent="9mm"/><fo:region-after extent="10mm"/></fo:simple-page-master></fo:layout-master-set>
      <fo:page-sequence master-reference="bank"><xsl:call-template name="header-footer"/>
        <fo:flow flow-name="xsl-region-body">
          <fo:block font-size="9pt" color="#167B86" space-after="3mm">01 / ЗАЯВЛЕНИЕ И УСЛОВИЯ</fo:block>
          <fo:block font-size="25pt" line-height="1.15" color="#173B57" space-after="4mm">Пакет документов</fo:block>
          <fo:block font-size="16pt" color="#173B57" space-after="6mm">для подписания клиентом</fo:block>
          <fo:block background-color="#EEF5F8" padding="4mm" space-after="5mm">Документ <xsl:value-of select="@id"/><fo:leader leader-pattern="space"/>Дата: <xsl:value-of select="format-date(xs:date(@issued),'[D01].[M01].[Y0001]')"/></fo:block>
          <fo:block xsl:use-attribute-sets="heading">Данные клиента</fo:block>
          <fo:block font-size="13pt" space-after="2mm"><xsl:value-of select="client/@name"/></fo:block>
          <fo:block>Дата рождения: <xsl:value-of select="format-date(xs:date(client/@birth-date),'[D01].[M01].[Y0001]')"/></fo:block>
          <fo:block>Паспорт: <xsl:value-of select="client/@passport"/> (тестовое значение)</fo:block>
          <fo:block>Телефон: <xsl:value-of select="client/@phone"/></fo:block>
          <fo:block>Адрес: <xsl:value-of select="client/@address"/></fo:block>
          <fo:block xsl:use-attribute-sets="heading">Расчётные параметры</fo:block>
          <fo:table table-layout="fixed" width="100%"><fo:table-column column-width="106mm"/><fo:table-column column-width="70mm"/><fo:table-body>
            <fo:table-row><fo:table-cell xsl:use-attribute-sets="cell"><fo:block>Сумма</fo:block></fo:table-cell><fo:table-cell xsl:use-attribute-sets="cell"><fo:block text-align="right"><xsl:value-of select="b:money($principal)"/> руб.</fo:block></fo:table-cell></fo:table-row>
            <fo:table-row><fo:table-cell xsl:use-attribute-sets="cell"><fo:block>Срок / тестовая годовая ставка</fo:block></fo:table-cell><fo:table-cell xsl:use-attribute-sets="cell"><fo:block text-align="right"><xsl:value-of select="$months"/> мес. / <xsl:value-of select="b:money(xs:decimal(loan/@annual-rate))"/> %</fo:block></fo:table-cell></fo:table-row>
            <fo:table-row><fo:table-cell xsl:use-attribute-sets="cell"><fo:block>Проценты по тестовому графику</fo:block></fo:table-cell><fo:table-cell xsl:use-attribute-sets="cell"><fo:block text-align="right"><xsl:value-of select="b:money($interest)"/> руб.</fo:block></fo:table-cell></fo:table-row>
            <fo:table-row background-color="#EEF5F8"><fo:table-cell xsl:use-attribute-sets="cell"><fo:block>Всего по графику</fo:block></fo:table-cell><fo:table-cell xsl:use-attribute-sets="cell"><fo:block text-align="right"><xsl:value-of select="b:money($principal+$interest)"/> руб.</fo:block></fo:table-cell></fo:table-row>
          </fo:table-body></fo:table>
          <fo:block space-before="6mm" font-size="9pt" color="#526777">Тестовый алгоритм: равные доли основного долга; проценты за месяц = остаток × ставка / 12. Календарные дни, переносы выходных и банковские правила округления не моделируются. Расчёт используется только для проверки генератора.</fo:block>
          <fo:block space-before="5mm">Комплект: заявление и условия; график платежей; выбранные услуги, подтверждения и поля подписей.</fo:block>
          <fo:block space-before="8mm" border-top="0.5pt solid #DCE3EA" padding-top="3mm" font-size="9pt">Сведения подготовлены для визуальной и функциональной проверки приложения. Это демонстрационный документ.</fo:block>
        </fo:flow>
      </fo:page-sequence>
      <fo:page-sequence master-reference="bank"><xsl:call-template name="header-footer"/>
        <fo:flow flow-name="xsl-region-body">
          <fo:block font-size="9pt" color="#167B86" space-after="3mm">02 / ПРИЛОЖЕНИЕ К ДОКУМЕНТУ</fo:block>
          <fo:block font-size="24pt" color="#173B57" space-after="3mm">График платежей</fo:block>
          <fo:block space-after="6mm"><xsl:value-of select="client/@name"/> / <xsl:value-of select="@id"/></fo:block>
          <xsl:call-template name="b:reset-numbers"/>
          <fo:table table-layout="fixed" width="100%" font-size="8pt"><fo:table-column column-width="9mm"/><fo:table-column column-width="27mm"/><fo:table-column column-width="35mm"/><fo:table-column column-width="31mm"/><fo:table-column column-width="35mm"/><fo:table-column column-width="39mm"/>
            <fo:table-header background-color="#173B57" color="white"><fo:table-row>
              <xsl:for-each select="('№','Дата','Основной долг','Проценты','Платёж','Остаток')"><fo:table-cell padding="2mm"><fo:block><xsl:value-of select="."/></fo:block></fo:table-cell></xsl:for-each>
            </fo:table-row></fo:table-header>
            <fo:table-body>
              <xsl:for-each select="1 to $months"><xsl:variable name="i" select="."/><xsl:variable name="part" select="$principal div $months"/><xsl:variable name="charge" select="b:interest($loan,$i)"/>
                <fo:table-row background-color="{if ($i mod 2=0) then '#F2F6F8' else '#FFFFFF'}" keep-together.within-page="always">
                  <fo:table-cell xsl:use-attribute-sets="cell"><fo:block><xsl:value-of select="b:row-number($i)"/></fo:block></fo:table-cell>
                  <fo:table-cell xsl:use-attribute-sets="cell"><fo:block><xsl:value-of select="format-date(b:due-date(xs:date($doc/@issued),$i),'[D01].[M01].[Y0001]')"/></fo:block></fo:table-cell>
                  <xsl:for-each select="($part, $charge, $part + $charge, $principal - $part * $i)"><fo:table-cell xsl:use-attribute-sets="cell"><fo:block text-align="right"><xsl:value-of select="b:money(.)"/></fo:block></fo:table-cell></xsl:for-each>
                </fo:table-row>
              </xsl:for-each>
              <fo:table-row background-color="#DCEAF0"><fo:table-cell number-columns-spanned="2" xsl:use-attribute-sets="cell"><fo:block>ИТОГО</fo:block></fo:table-cell><xsl:for-each select="($principal,$interest,$principal+$interest,xs:decimal(0))"><fo:table-cell xsl:use-attribute-sets="cell"><fo:block text-align="right"><xsl:value-of select="b:money(.)"/></fo:block></fo:table-cell></xsl:for-each></fo:table-row>
            </fo:table-body>
          </fo:table>
          <fo:block space-before="5mm" font-size="9pt" color="#526777">Все суммы в рублях. Даты считаются от даты документа добавлением календарных месяцев. Выбранные услуги из раздела 03 в платежи не включены.</fo:block>
          <fo:block space-before="10mm">С графиком ознакомлен:</fo:block>
          <fo:block space-before="8mm">Подпись клиента ____________________ / <xsl:value-of select="client/@name"/></fo:block>
        </fo:flow>
      </fo:page-sequence>
      <fo:page-sequence master-reference="bank"><xsl:call-template name="header-footer"/>
        <fo:flow flow-name="xsl-region-body">
          <fo:block font-size="9pt" color="#167B86" space-after="3mm">03 / ПОДТВЕРЖДЕНИЯ</fo:block>
          <fo:block font-size="24pt" color="#173B57" space-after="5mm">Согласования и подписи</fo:block>
          <fo:block xsl:use-attribute-sets="heading">Выбранные услуги</fo:block>
          <xsl:for-each select="services/service[@selected='true']"><fo:block space-after="2mm"><xsl:value-of select="concat(position(), '. ', ., ' - ', b:money(xs:decimal(@amount)), ' руб.')"/></fo:block></xsl:for-each>
          <fo:block background-color="#EEF5F8" padding="4mm" space-before="3mm">Итого отдельно выбранные услуги: <xsl:value-of select="b:money(b:service-total(.))"/> руб.</fo:block>
          <fo:block xsl:use-attribute-sets="heading">Проверка комплекта</fo:block>
          <xsl:call-template name="b:clauses"/>
          <fo:block space-before="4mm" font-size="9pt" color="#526777">Повторяющиеся примечания из входного XML выведены один раз. Поля ниже предназначены для проверки верстки; подписи не проставлены.</fo:block>
          <fo:block xsl:use-attribute-sets="heading">Подписание</fo:block>
          <fo:block space-before="4mm">Клиент: <xsl:value-of select="client/@name"/></fo:block>
          <fo:block space-before="10mm">Подпись ____________________  Дата ____________________</fo:block>
          <fo:block space-before="10mm">Представитель банка __________________________________</fo:block>
          <fo:block space-before="10mm">Подпись ____________________  Дата ____________________</fo:block>
          <fo:block space-before="10mm" border-top="0.5pt solid #DCE3EA" padding-top="4mm" font-size="9pt">Комплект получен клиентом: ____________________<fo:block space-before="2mm">Идентификатор: <xsl:value-of select="@id"/>. Страниц по сценарию: 3.</fo:block></fo:block>
        </fo:flow>
      </fo:page-sequence>
    </fo:root>
  </xsl:template>
</xsl:stylesheet>
